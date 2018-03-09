@echo off
cls

.paket\paket.exe restore
if errorlevel 1 (
  exit /b %errorlevel%
)

packages\build\FAKE\tools\FAKE.exe build.fsx Deploy "DockerLoginServer=docker.io" "DockerImageName=****" "DockerUser=jackyrul" "DockerPassword=7924655q" %*