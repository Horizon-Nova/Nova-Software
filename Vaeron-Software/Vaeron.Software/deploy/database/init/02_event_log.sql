-- 發生過的事：什麼時候、什麼事情、系統怎麼處理、誰；一件一列，只新增不改寫，全部留著

CREATE TABLE dbo.event_log (
    id            serial PRIMARY KEY,
    type          character varying(8) NOT NULL,
    tool_no       character varying(255),
    account       character varying(20),
    function_name character varying(2047),
    message       text,
    stack_trace   text,
    created_at    timestamp with time zone NOT NULL DEFAULT now(),
    updated_at    timestamp with time zone NOT NULL DEFAULT now()
);

COMMENT ON TABLE dbo.event_log IS '發生過的事：每一次啟用、開啟、解鎖設定工具，每一個沒被接住的錯誤，一件一列，只新增不改寫，全部留著';
COMMENT ON COLUMN dbo.event_log.id IS '流水號';
COMMENT ON COLUMN dbo.event_log.type IS 'activate＝啟用工具；open＝開啟工具；unlock＝解鎖工具設定；error＝沒被接住的錯誤';
COMMENT ON COLUMN dbo.event_log.tool_no IS '哪一支工具；不是對著工具發生的是空的';
COMMENT ON COLUMN dbo.event_log.account IS '誰：當時登入 Windows 的帳號；Windows 帳號上限 20';
COMMENT ON COLUMN dbo.event_log.function_name IS '系統由哪一支方法處理：型別.方法；錯誤時是例外的 TargetSite。.NET 型別全名 1023 ＋ 方法名 1023';
COMMENT ON COLUMN dbo.event_log.message IS '處理的結果；[error] 錯誤訊息';
COMMENT ON COLUMN dbo.event_log.stack_trace IS '[error] 呼叫堆疊';
COMMENT ON COLUMN dbo.event_log.created_at IS '發生的時刻';
COMMENT ON COLUMN dbo.event_log.updated_at IS '這一列最後修改的時刻';
