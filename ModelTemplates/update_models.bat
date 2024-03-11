@echo off
del ModelGenerationLog.log
del ..\*.tt_generated.* /s
"%VSAPPIDDIR%texttransform.exe" models.tt -out "models.cs"
type ModelGenerationLog.log