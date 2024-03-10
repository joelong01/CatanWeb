@echo off
del ModelGenerationLog.log
"%VSAPPIDDIR%texttransform.exe" models.tt -out "models.cs"
type ModelGenerationLog.log