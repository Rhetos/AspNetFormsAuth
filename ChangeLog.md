# Rhetos.AspNetFormsAuth release notes

## 5.2.0 (2022-07-06)

* Runtime assets (AdminSetup.exe and other files) hidden in Visual Studio Solution Explorer to reduce clutter.

## 5.1.0 (2022-04-08)

* .NET 6 support for CLI utility AdminSetup.exe.

## 5.0.0 (2022-03-25)

### Breaking changes

1. Migrated from .NET Framework to .NET 5 and Rhetos 5.
2. When calling the `Resources/AspNetFormsAuth/Authentication/ResetPassword` web method,
   the *UserName* parameter value should also be passed along with the old parameters *PasswordResetToken* and *NewPassword*.
3. The *TokenExpirationInMinutesFromNow* parameter is no longer used in the `Resources/AspNetFormsAuth/Authentication/GeneratePasswordResetToken` web method.
   Also, `AspNetFormsAuth.SendPasswordResetToken.ExpirationInMinutes` appSettings key is no longer used in the `SendPasswordResetToken` web method.
   To configure the token expiration time, set
   the [DataProtectionTokenProviderOptions.TokenLifespan](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.dataprotectiontokenprovideroptions.tokenlifespan?view=aspnetcore-5.0)
   property inside the Startup.ConfigureServices method of your web application.
4. Removed the option to set the password attempts limit through database (table `Common.AspNetFormsAuthPasswordAttemptsLimit`).
   Password attempt limits and lockout time can be customized in
   the [IdentityOptions.LockoutOptions](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.identityoptions.lockout?view=aspnetcore-5.0#Microsoft_AspNetCore_Identity_IdentityOptions_Lockout)
   class.
5. AdminSetup.exe CLI parameters are changed:
   1. It requires a relative path to the host application's startup assembly as a CLI argument.
   2. Instead of `/nopause` option, use `--no-pause`.
   3. Instead of `-pass` option, use `--password`.
