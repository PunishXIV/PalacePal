# Palace Pal

## Client Build Notes

### Database Migrations

Since EF core needs all dll files to be present, including Dalamud ones,
there's a special `EF` configuration that exempts them from setting
`<Private>false</Private>` during the build.

To use with `dotnet ef` commands, specify it as `-c EF`, for example:

```shell
dotnet ef migrations add MigrationName --configuration EF
```

To rebuild the compiled model:
```shell
dotnet ef dbcontext optimize --output-dir Database/Compiled --namespace Pal.Client.Database.Compiled --configuration EF
```
