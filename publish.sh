cd PaperlessCopier
dotnet publish PaperlessCopier.csproj -r linux-x64 -c Release /p:PublishSingleFile=true /p:PublishTrimmed=true -p:IncludeNativeLibrariesForSelfExtract=true
