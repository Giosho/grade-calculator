# Сглобява Калкулатор за оценки.exe (нужен е само Windows; csc.exe е вграден в .NET Framework).
# Първо сваля пакета Microsoft.Web.WebView2 (DLL файловете за вградения браузър) в папка pkg, ако го няма.
param([string]$Out = "$env:USERPROFILE\Desktop\Калкулатор за оценки.exe")
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
if (-not (Test-Path 'pkg\x\lib\net462\Microsoft.Web.WebView2.Core.dll')) {
    New-Item -ItemType Directory -Force pkg | Out-Null
    $ProgressPreference = 'SilentlyContinue'
    Invoke-WebRequest 'https://www.nuget.org/api/v2/package/Microsoft.Web.WebView2' -OutFile 'pkg\wv2.zip'
    Expand-Archive 'pkg\wv2.zip' 'pkg\x' -Force
}
$l = 'pkg\x\lib\net462'
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /target:winexe /platform:x64 /codepage:65001 "/out:$Out" /win32icon:app.ico /resource:app.ico,app.ico `
  "/resource:$l\Microsoft.Web.WebView2.Core.dll,wv2core.dll" "/resource:$l\Microsoft.Web.WebView2.WinForms.dll,wv2wf.dll" `
  "/resource:pkg\x\runtimes\win-x64\native\WebView2Loader.dll,wv2loader.dll" `
  "/reference:$l\Microsoft.Web.WebView2.Core.dll" "/reference:$l\Microsoft.Web.WebView2.WinForms.dll" `
  /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll `
  Program.cs MainForm.cs Theme.cs Store.cs Controls.cs Views.cs Shkolo.cs
if ($LASTEXITCODE -ne 0) { throw "Грешка при компилиране" }
"Готово: $Out"
