@echo off
del ModelGenerationLog.log
del *.tt_generated.* /s
del GenerateModels.txt
..\ModelGenerator\bin\Debug\net8.0\ModelGenerator.exe .
type ModelGenerationLog.log
echo DONE