@echo off
CHCP 65001

set dir=%~dp0

set moduleName=system
set apiServicesPath=%dir%..\src\api-services\system\
set apiUrl=http://localhost:5005/swagger/Default/swagger.json

if "%1"=="dataApproval" (
  set moduleName=dataApproval
  set apiServicesPath=%dir%..\src\api-services\dataApproval\
  set apiUrl=http://localhost:5005/swagger/DataApproval/swagger.json
) else if "%1"=="dingTalk" (
  set moduleName=dingTalk
  set apiServicesPath=%dir%..\src\api-services\dingTalk\
  set apiUrl=http://localhost:5005/swagger/DingTalk/swagger.json
) else if "%1"=="goView" (
  set moduleName=goView
  set apiServicesPath=%dir%..\src\api-services\goView\
  set apiUrl=http://localhost:5005/swagger/GoView/swagger.json
) else if "%1"=="aiChat" (
  set moduleName=aiChat
  set apiServicesPath=%dir%..\src\api-services\aiChat\
  set apiUrl=http://localhost:5005/swagger/AiChat/swagger.json
) else if "%1"=="paddleOCR" (
  set moduleName=paddleOCR
  set apiServicesPath=%dir%..\src\api-services\paddleOCR\
  set apiUrl=http://localhost:5005/swagger/PaddleOCR/swagger.json
)

if exist %apiServicesPath% (
    echo ================================ 删除目录 %moduleName% ================================
    rd /s /q %apiServicesPath%
)

echo ================================ 开始生成 %moduleName% ================================

java -jar %dir%swagger-codegen-cli.jar generate -i %apiUrl% -l typescript-axios -o %apiServicesPath%

@rem 删除不必要的文件和文件夹
rd /s /q %apiServicesPath%.swagger-codegen
del /q %apiServicesPath%.gitignore
del /q %apiServicesPath%.npmignore
del /q %apiServicesPath%.swagger-codegen-ignore
del /q %apiServicesPath%git_push.sh
del /q %apiServicesPath%package.json
del /q %apiServicesPath%README.md
del /q %apiServicesPath%tsconfig.json

echo ================================ 生成结束 ================================