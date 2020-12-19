# User must have .NET Core installed
dotnet publish -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true

# Packages all of .NET Core with the application
#dotnet publish -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true
