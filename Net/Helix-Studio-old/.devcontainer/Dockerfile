# windows/servercore：有完整 API/子系統可以執行 GUI 應用邏輯，只是預設看不到畫面。
# 查證見 security.md／準則第 19 條——不是隨便選的 base image。
FROM mcr.microsoft.com/windows/servercore:ltsc2022
RUN powershell -Command \
    Invoke-WebRequest -Uri https://dot.net/v1/dotnet-install.ps1 -OutFile dotnet-install.ps1; \
    ./dotnet-install.ps1 -Channel 8.0 -InstallDir C:\dotnet
ENV PATH="C:\dotnet;${PATH}"
WORKDIR C:\workspace
