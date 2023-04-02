dotnet publish -c Release -r win-x64 --self-contained
sc.exe create "Lst.Svc" binpath="bin\Release\net7.0\win-x64\publish\Lst.WindowsService.exe" start=auto