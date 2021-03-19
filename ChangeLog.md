# Rhetos.AspNetFormsAuth release notes

## 5.0.0 (TO BE RELEASED)

### Breaking changes

1. When calling the `Resources/AspNetFormsAuth/Authentication/ResetPassword` method the UserName value should also be passed along with the PasswordResetToken and NewPassword.
2. The TokenExpirationInMinutesFromNow is no longer used in the `Resources/AspNetFormsAuth/Authentication/GeneratePasswordResetToken` method.
   To set the token expiration time, set the `DataProtectionTokenProviderOptions.TokenLifespan` property inside the ConfigureServices method.
3. Removed the option to set the password attempt limits through database. Password attempt limits and lockout time can be customized in the `IdentityOptions.LockoutOptions` class.
