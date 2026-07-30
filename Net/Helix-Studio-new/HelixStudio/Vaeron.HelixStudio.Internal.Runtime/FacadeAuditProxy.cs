using System.Reflection;
using Vaeron.Kernel;
using Vaeron.Kernel.Guarding;

namespace Vaeron.HelixStudio.Internal.Runtime;

/// <summary>把任一 Facade 介面包成攔截代理：呼叫方完全不用改,業務程式碼裡不寫任何一行記錄呼叫——
/// 每個方法呼叫（成功／Result.Fail／例外）自動分送給每一個已註冊的 IAuditSink（見該介面）。
/// 多個 Sink 各自獨立：本地文字檔（AuditLogging）與 EventLog 資料庫（EventLogAuditSink）
/// 是兩條平行、互不依賴的寫入路徑，不是「一個完成才觸發另一個」的串接關係——任一個沒註冊、
/// 寫入失敗或耗時,都不影響另一個是否收到、何時收到這筆記錄。
///
/// 例外只記錄、不吞：記完照原樣往上丟，呼叫方拿到的行為與沒有這層代理時完全一樣。
/// 這裡刻意不用 try/catch——ContinueWith(...).Unwrap() 直接檢查已完成 Task 的狀態,不攔截例外,
/// 不受 VAE002（catch 只能在能回傳 Result 的地方)限制,語意上也更準確：這是「觀察」不是「決定」。
/// 同步方法無法用 Task 的狀態機制觀察失敗,改用 Guard.Run 的失敗處置委派記錄後原樣重新拋出。</summary>
public static class FacadeAuditProxy
{
    public static TInterface Wrap<TInterface>(TInterface target, IEnumerable<IAuditSink> sinks) where TInterface : class
    {
        var proxy = DispatchProxy.Create<TInterface, Interceptor<TInterface>>();
        ((Interceptor<TInterface>)(object)proxy).Attach(target, sinks);
        return proxy;
    }

    // DispatchProxy 的執行期產生器會在這個型別底下動態衍生子類別,TProxy 不能是 sealed
    // （DispatchProxy.Create 內部要求可繼承,否則 ArgumentException: "The base type ... cannot be sealed"）。
    private class Interceptor<TInterface> : DispatchProxy where TInterface : class
    {
        private TInterface _target = default!;
        private IReadOnlyList<IAuditSink> _sinks = [];
        private string _facadeName = "";

        public void Attach(TInterface target, IEnumerable<IAuditSink> sinks)
        {
            _target = target;
            _sinks = sinks.ToList();
            _facadeName = typeof(TInterface).Name;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null) return null;

            // 事件訂閱／解除（add_X／remove_X）直接穿透,不當成一次「操作」記錄
            if (targetMethod.IsSpecialName)
                return targetMethod.Invoke(_target, args);

            if (typeof(Task).IsAssignableFrom(targetMethod.ReturnType))
                return InvokeAsync(targetMethod, args);

            return InvokeSync(targetMethod, args);
        }

        private object? InvokeSync(MethodInfo targetMethod, object?[]? args)
        {
            object? result = null;
            Exception? failure = null;

            Guard.Run(() => result = targetMethod.Invoke(_target, args), ex =>
            {
                failure = ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;
            });

            if (failure is not null)
            {
                LogException(targetMethod.Name, failure);
                throw failure;
            }

            LogOutcome(targetMethod.Name, result);
            return result;
        }

        private object InvokeAsync(MethodInfo targetMethod, object?[]? args)
        {
            // 非同步方法的同步前導段（例如 await 之前的參數檢查）仍可能同步丟例外,尚未進入 Task——
            // 同樣用 Guard 記錄後原樣重新拋出,不用 try/catch（VAE002）。
            Task? task = null;
            Exception? prologueFailure = null;
            Guard.Run(() => task = (Task)targetMethod.Invoke(_target, args)!, ex =>
                prologueFailure = ex is TargetInvocationException { InnerException: { } inner } ? inner : ex);

            if (prologueFailure is not null)
            {
                LogException(targetMethod.Name, prologueFailure);
                throw prologueFailure;
            }

            var resultType = targetMethod.ReturnType.IsGenericType
                ? targetMethod.ReturnType.GetGenericArguments()[0]
                : null;

            if (resultType is null)
                return ContinueVoidTask(task!, targetMethod.Name);

            var continuation = typeof(Interceptor<TInterface>)
                .GetMethod(nameof(ContinueTypedTask), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(resultType);
            return continuation.Invoke(this, [task!, targetMethod.Name])!;
        }

        private Task ContinueVoidTask(Task antecedent, string methodName)
            => antecedent.ContinueWith(t =>
            {
                if (t.IsFaulted) LogException(methodName, t.Exception!.GetBaseException());
                else if (!t.IsCanceled) LogOutcome(methodName, null);
                return t;
            }, TaskContinuationOptions.ExecuteSynchronously).Unwrap();

        private Task<T> ContinueTypedTask<T>(Task task, string methodName)
        {
            var typed = (Task<T>)task;
            return typed.ContinueWith(t =>
            {
                if (t.IsFaulted) LogException(methodName, t.Exception!.GetBaseException());
                else if (!t.IsCanceled) LogOutcome(methodName, t.Result);
                return t;
            }, TaskContinuationOptions.ExecuteSynchronously).Unwrap();
        }

        private void LogOutcome(string methodName, object? value)
        {
            var hasResult = TryDescribeResult(value, out var isSuccess, out var message);
            var description = hasResult
                ? $"{methodName} -> {(isSuccess ? "OK" : "FAIL")} {message}".TrimEnd()
                : $"{methodName} -> OK";
            Dispatch(new AuditEntry(_facadeName, description, IsFailure: hasResult && !isSuccess));
        }

        private void LogException(string methodName, Exception ex)
            => Dispatch(new AuditEntry(_facadeName, $"{methodName} -> EXCEPTION {ex.GetType().Name}: {ex.Message}", ex, IsFailure: true));

        // 每個 Sink 各自獨立送出：不等待、不排序、彼此互不依賴——一個 Sink 沒註冊或寫入失敗，
        // 完全不影響其他 Sink 是否收到這筆記錄，也不影響被攔截的原呼叫本身的回傳時機。
        private void Dispatch(AuditEntry entry)
        {
            foreach (var sink in _sinks)
                _ = Guard.RunAsync(() => sink.CaptureAsync(entry), _ => { });
        }

        private static bool TryDescribeResult(object? value, out bool isSuccess, out string message)
        {
            if (value is Result r)
            {
                isSuccess = r.IsSuccess;
                message = r.Message;
                return true;
            }

            var type = value?.GetType();
            if (type is { IsGenericType: true } && type.GetGenericTypeDefinition() == typeof(Result<>))
            {
                isSuccess = (bool)type.GetProperty(nameof(Result.IsSuccess))!.GetValue(value)!;
                message = (string)type.GetProperty(nameof(Result.Message))!.GetValue(value)!;
                return true;
            }

            isSuccess = true;
            message = "";
            return false;
        }
    }
}
