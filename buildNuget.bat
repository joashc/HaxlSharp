move /Y nuget\*.nupkg nuget\previous
nuget pack HaxlSharp.Core\HaxlSharp.Core.csproj -Prop Configuration=Release -OutputDirectory nuget\
nuget pack HaxlSharp.Fetcher\HaxlSharp.Fetcher.csproj -Prop Configuration=Release -IncludeReferencedProjects -OutputDirectory nuget\
pause
