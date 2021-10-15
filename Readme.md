# AspNetFormsAuth

AspNetFormsAuth is a plugin package for [Rhetos development platform](https://github.com/Rhetos/Rhetos).
It provides an implementation of **ASP.NET Core Identity authentication** to Rhetos server applications.

The authentication is implemented using Microsoft's *WebMatrix SimpleMembershipProvider*,
with recommended security best practices such as password salting and hashing.
Implementation fully depends on SimpleMembershipProvider; AspNetFormsAuth project does not try
to implement its own authentication or security mechanisms.

Table of contents:

1. [Features](#features)
   1. [Authentication](#authentication)
   2. [Common administration activities](#common-administration-activities)
   3. [Forgot password](#forgot-password)
2. [Authentication service API](#authentication-service-api)
   1. [Login](#login)
   2. [Logout](#logout)
   3. [SetPassword](#setpassword)
   4. [ChangeMyPassword](#changemypassword)
   5. [UnlockUser](#unlockuser)
   6. [GeneratePasswordResetToken](#generatepasswordresettoken)
   7. [SendPasswordResetToken](#sendpasswordresettoken)
   8. [ResetPassword](#resetpassword)
3. [Installation](#installation)
   1. [Hosting web app on IIS](#hosting-web-app-on-iis)
   2. [Set up HTTPS](#set-up-https)
4. [Configuration](#configuration)
   1. [Admin user password](#admin-user-password)
   2. [Permissions and claims](#permissions-and-claims)
   3. [Maximum failed password attempts](#maximum-failed-password-attempts)
   4. [Password strength policy](#password-strength-policy)
5. [Uninstallation](#uninstallation)
6. [Sharing the authentication across web applications](#sharing-the-authentication-across-web-applications)
7. [Session timeout](#session-timeout)
8. [Implementing SendPasswordResetToken](#implementing-sendpasswordresettoken)
   1. [Custom implementation](#custom-implementation)
9. [Troubleshooting](#troubleshooting)
10. [Build](#build)

## Features

### Authentication

* [Authentication service](#authentication-service-api) may be used in web applications
  and other services to login and logout users, and for other related actions.
* Forms authentication may be utilized for [sharing the authentication](#sharing-the-authentication-across-web-applications)
  across multiple web applications.

### Common administration activities

* To create a new user, insert the record in the `Common.Principal` entity.
* To configure the user's permissions, enter the data in `Common.PrincipalHasRole` or `Common.PrincipalPermission`.
* To set the user's password, the administrator may use [`SetPassword`](#setpassword)
  or [`GeneratePasswordResetToken`](#generatepasswordresettoken)  web service methods (see below).
  The user will later use [`ResetPassword`](#resetpassword) with the password reset token,
  or [`ChangeMyPassword`](#changemypassword) when logged-in.

### Forgot password

There are two recommended ways of implementing *forgot password* functionality with AspNetFormsAuth:

* Option 1: An administrator (or a web application *with administrator privileges*) may call
  [`GeneratePasswordResetToken`](#generatepasswordresettoken) web method to get the user's password reset token.
  The administrator or the web application should then send the token to the user on its own.

* Option 2: An end user that is not logged-in (or a web application *with no special privileges*) may call
  [`SendPasswordResetToken`](#sendpasswordresettoken) web method. The Rhetos sever will generate
  the password reset token and send it to the user. In order to use this method,
  an implementation of sending the token (by SMS or email, e.g.) should be provided by an additional plugin
  (see [Implementing SendPasswordResetToken](#implementing-sendpasswordresettoken)).

## Authentication service API

The JSON service is available at URI `<rhetos server>/Resources/AspNetFormsAuth/Authentication`, with the following methods.

### Login

* Interface: `(string UserName, string Password, bool PersistCookie) -> bool`
* Example of the request data: `{"UserName":"myusername","Password":"mypassword","PersistCookie":false}`.
* The method does not require user authentication.
* On successful log in, the server response will contain the standard authentication cookie.
  The client browser will automatically use the cookie for following requests.
* Response data is boolean *true* if the login is successful,
  *false* if the login and password does not match,
  or an error message (string) with HTTP error code 4* or 5* in case of any other error.

### Logout

* No request data is needed, assuming standard authentication cookie is automatically provided. Response is empty.

### SetPassword

Sets or resets the given user's password.

* Interface: `(string UserName, string Password, bool IgnorePasswordStrengthPolicy) -> void`
* Requires `SetPassword` [security claim](#permissions-and-claims).
  If IgnorePasswordStrengthPolicy property is set, `IgnorePasswordStrengthPolicy` [security claim](#permissions-and-claims) is required.
* Response data is empty if the command is successful,
  an error message (string) with HTTP error code 400 if the password does not match the password strength policy,
  or an error message with HTTP error code 4* or 5* in case of any other error.

### ChangeMyPassword

Changes the current user's password.

* Interface: `(string OldPassword, string NewPassword) -> bool`
* Response data is boolean *true* if the login is successful,
  *false* if the login and password does not match,
  an error message (string) with HTTP error code 400 if the password does not match the password strength policy,
  or an error message with HTTP error code 4* or 5* in case of any other error.

### UnlockUser

Reset the number of [failed login attempts](#maximum-failed-password-attempts).

* Interface: `(string UserName) -> void`
* Response is empty.
* Requires `UnlockUser` [security claim](#permissions-and-claims).

### GeneratePasswordResetToken

Generates a password reset token.

* Interface: `(string UserName) -> string`
* This method is typically called by an administrator or a web application with administrator privileges
  in order to create a user account without initial password and let a user choose it, or to implement forgot-password functionality.
* To implement forgot-password functionality *without* using administrator privileges in web application,
  use [`SendPasswordResetToken`](#sendpasswordresettoken) method instead (see [Forgot password](#forgot-password)).
* Requires `GeneratePasswordResetToken` [security claim](#permissions-and-claims).
* To configure the token expiration time, set
  the [DataProtectionTokenProviderOptions.TokenLifespan](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.dataprotectiontokenprovideroptions.tokenlifespan?view=aspnetcore-5.0)
  property inside the Startup.ConfigureServices method of your web application.

### SendPasswordResetToken

Generates a password reset token and sends it to the user.

* Interface: `(string UserName, Dictionary<string, string> AdditionalClientInfo) -> void`
* When using this method there is no need to directly call [`GeneratePasswordResetToken`](#generatepasswordresettoken)
  method (see [Forgot password](#forgot-password)).
* The method does not require user authentication.
* **NOTE:** *AspNetFormsAuth* package **does not contain** any implementation of sending  the token (by SMS or email, e.g.).
  The implementation must be provided by an additional plugin. For example:
  * Use the [SimpleSPRTEmail](https://github.com/Rhetos/SimpleSPRTEmail) plugin package for sending token by email,
  * or follow [Implementing SendPasswordResetToken](#implementing-sendpasswordresettoken) to implement a different sending method.
* To configure the token expiration time, set
  the [DataProtectionTokenProviderOptions.TokenLifespan](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.dataprotectiontokenprovideroptions.tokenlifespan?view=aspnetcore-5.0)
  property inside the Startup.ConfigureServices method of your web application.

### ResetPassword

Allows a user to set the initial password or reset the forgotten password, using the token he received previously.

* Interface: `(string userName, string PasswordResetToken, string NewPassword) -> bool`
* See `GeneratePasswordResetToken` method for *PasswordResetToken*.
* The method does not require user authentication.
* Response data is boolean *true* if the password change is successful,
  *false* if the token is invalid or expired,
  or an error message (string) with HTTP error code 4* or 5* in case of any other error.

## Installation

1. Add "Rhetos.AspNetFormsAuth" NuGet package, available at the [NuGet.org](https://www.nuget.org/) on-line gallery.

2. Modify lines in `Startup.cs`, method `ConfigureServices` to:

   ```cs
   services.AddRhetos(ConfigureRhetosHostBuilder)
       .AddAspNetFormsAuth();
   ```

3. Add to `Startup.cs`, method `Configure` **before** line `app.UseAuthentication()`:

   ```cs
   app.UseRhetosAspNetFormsAuth();
   ```

  * Make sure that you **don't** have this lines in `Startup.cs`, method `Configure`:

      ```cs
      services.AddAuthentication(...
      ```

  * If you want to show authentication APIs in Swagger, add this line in `Startup.cs`, method `Configure`:

      ```cs
      app.UseSwaggerUI(c =>
      {
        c.SwaggerEndpoint("/swagger/rhetos/swagger.json", "Rhetos REST API");
      });
      ```

### Hosting web app on IIS

If you need to host a Rhetos web app with forms authentication on IIS:

1. You could publish your application by use the feature Publish project in Visual Studio or you also use [dotnet cli to publish](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-publish) it instead.
2. Install latest [.NET Core module](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/aspnet-core-module) for IIS.
3. Create your web application on [IIS](https://docs.microsoft.com/en-us/iis/configuration/system.applicationhost/sites/site/application/).
   * Start IIS Manager -> Select the web application -> Open "Authentication" feature, make sure you set this:
     **enable** *Anonymous Authentication*,
     **disable** *Windows Authentication*, *Forms Authentication* and every other.

### Set up HTTPS

HTTPS (or any other) secure transport protocol **should always be enforced** when using forms authentication.
This is necessary because in forms authentication the **user's password** must be submitted from the client securely.
At least the services inside `/Resources/AspNetFormsAuth` path must use HTTPS.

To enable HTTPS, follow the instructions in [Setting up Rhetos for HTTPS](https://github.com/Rhetos/Rhetos/wiki/Setting-up-Rhetos-for-HTTPS).

Consider using a [free SSL certificate](https://www.google.hr/search?q=free+SSL+certificate)
in development or QA environment.

## Configuration

### Admin user password

Note: When deploying the AspNetFormsAuth packages, it will automatically create
the *admin* user account and *SecurityAdministrator* role, add the account to the role
and give it necessary permissions (claims) for all authentication service methods.

After deployment:

* Run the Rhetos utility `bin\...\AdminSetup.exe` to initialize the *admin* user's password. Use the following command-line arguments:

  ```
  AdminSetup.exe <your app startup dll> --password <your password> --no-pause
  ```

### Permissions and claims

All claims related to the authentication service have resource=`AspNetFormsAuth.AuthenticationService`.
[Admin user](#admin-user-password) has all the necessary permissions (claims) for all authentication service methods.

### Maximum failed password attempts

Password attempt limits and lockout time can be customized in
   the [IdentityOptions.LockoutOptions](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.identityoptions.lockout?view=aspnetcore-5.0#Microsoft_AspNetCore_Identity_IdentityOptions_Lockout)

### Password strength policy

Use entity *Common.AspNetFormsAuthPasswordStrength* (*RegularExpression*, *RuleDescription*) to configure the policy.

* A new password must pass all the rules in *Common.AspNetFormsAuthPasswordStrength*.
* *RuleDescription* is uses as an error message to the user if the new password breaks the policy.
* When administrator executes [`SetPassword`](#setpassword) method, the property *IgnorePasswordStrengthPolicy*
  may be used to avoid the policy.

Examples:

RegularExpression|RuleDescription
-----------------|---------------
`.{6,}`          | The password length must be at least six characters.
`\d`             | The password must contain at least one digit.
`(\d.*){3,}`     | The password must contain at least three digits.
`[A-Z]`          | The password must contain at least one uppercase letters.
`\W`             | The password must contain at least one special character (not a letter or a digit).

## Uninstallation

1. Remove package Rhetos.AspNetFormsAuth from your project (`.csproj` file).

2. Remove `.AddAspNetFormsAuth` line in `Startup.cs`, method `ConfigureServices`.

3. Remove `app.UseRhetosAspNetFormsAuth()` line in `Startup.cs`, method `Configure`.

## Sharing the authentication across web applications

Sharing the authentication cookie is useful when using separate web applications for web pages and application services, or when using multiple servers for load balancing.
In these scenarios, sharing the forms authentication cookie between the sites will allow a single-point login for the user on any of the sites and seamless use of that cookie on the other sites.

You could check official document of Microsoft here [Share authentication cookies among ASP.NET apps](https://docs.microsoft.com/en-us/aspnet/core/security/cookie-sharing).

**Sharing the authentication with ASP.NET MVC (.NET Framework)**
- In case you have ASP.NET MVC "frontend" apps shared security with Rhetos app, please see this chapter [Share authentication cookies between ASP.NET 4.x and ASP.NET Core apps](https://docs.microsoft.com/en-us/aspnet/core/security/cookie-sharing?view=aspnetcore-5.0#share-authentication-cookies-between-aspnet-4x-and-aspnet-core-apps). This is the example code how to do it:

    ```cs
    // Rhetos app (.NET Core) config
    services.AddRhetos(ConfigureRhetosHostBuilder)
       .AddAspNetFormsAuth();

    ...

    services.PostConfigureAll<CookieAuthenticationOptions>(options =>
    {
        options.Cookie.Name = ".AspNet.SharedCookie";
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Path = "/";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(120);
        options.CookieManager = new ChunkingCookieManager();
        options.TicketDataFormat = new SecureDataFormat<AuthenticationTicket>(
            new TicketSerializer(),
            DataProtectionProvider.Create(
                new DirectoryInfo("C:\\keyring"),
                (builder) => { builder.SetApplicationName("iis-app-name"); }
            ).CreateProtector(
                "Microsoft.AspNetCore.Authentication.Cookies." +
                "CookieAuthenticationMiddleware",
                "Cookies.Application",
                "v2"
            )
        );
    });
    ```

    ```cs
    // ASP.NET MVC (.NET Framework) config
    app.UseCookieAuthentication(new CookieAuthenticationOptions()
    {
        AuthenticationType = CookieAuthenticationDefaults.AuthenticationType,
        CookieName = ".AspNet.SharedCookie",
        SlidingExpiration = true,
        ExpireTimeSpan = TimeSpan.FromMinutes(120),
        LoginPath = PathString.FromUriComponent("/Account/Login"),
        LogoutPath = PathString.FromUriComponent("/Account/LogOff"),
        CookieManager = new ChunkingCookieManager(),
        TicketDataFormat = new AspNetTicketDataFormat(
            new DataProtectorShim(
                DataProtectionProvider.Create(
                    new DirectoryInfo("C:\\keyring"),
                    (builder) => { builder.SetApplicationName("iis-app-name"); }
                ).CreateProtector(
                    "Microsoft.AspNetCore.Authentication.Cookies." +
                    "CookieAuthenticationMiddleware",
                    "Cookies.Application",
                    "v2"
                )
            )
        )
    });
    ```

## Session timeout

Configure the ASP.NET Identity authentication ticket timeout with [CookieAuthenticationOptions.ExpireTimeSpan](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.builder.cookieauthenticationoptions.expiretimespan?view=aspnetcore-1.1#Microsoft_AspNetCore_Builder_CookieAuthenticationOptions_ExpireTimeSpan).
The default value is [14 days](https://github.com/dotnet/aspnetcore/blob/8b30d862de6c9146f466061d51aa3f1414ee2337/src/Security/Authentication/Cookies/src/CookieAuthenticationOptions.cs#L31).

## Implementing SendPasswordResetToken

In order to use [`SendPasswordResetToken`](#sendpasswordresettoken) web method (see also [Forgot password](#forgot-password)),
an additional plugin must be provided that sends the token to the user (by SMS or email, e.g.).

* A sample implementation is available at [https://github.com/Rhetos/SimpleSPRTEmail](https://github.com/Rhetos/SimpleSPRTEmail).
  This plugin package may be used for sending simple emails.

### Custom implementation

In order to implement a custom method of sending the token to the user (by SMS or email, e.g.),
create a Rhetos plugin package with a class that implements the `Rhetos.AspNetFormsAuth.ISendPasswordResetToken` interface
from `Rhetos.AspNetFormsAuth.Interfaces.dll`.
The class must use `Export` attribute to register the plugin implementation.
For example, [you could check the code here](https://github.com/Rhetos/AspNetFormsAuth/blob/master/test/Rhetos.AspNetFormsAuth.TestApp/Mocks/SendPasswordResetTokenMock.cs):

```C#
[Export(typeof(ISendPasswordResetToken))]
public class EmailSender : ISendPasswordResetToken
{
    ...
}
```

The `AdditionalClientInfo` parameter of web service method `/SendPasswordResetToken` will be provided to the implementation function.
The parameter may contain answers to security questions, preferred method of communication or any similar user provided information
required by the `ISendPasswordResetToken` implementation.

The implementation class may throw a `Rhetos.UserException` or a `Rhetos.ClientException` to provide an error message to the client,
but use it with caution, or better avoid it: The `SendPasswordResetToken` web service method allows **anonymous access**,
so providing any error information to the client might be a security issue.

Any other exception (`Rhetos.FrameworkException`, e.g.) will only be logged on the server, but no error will be sent to the client.

## Troubleshooting

**Issue**: Deployment results with error message "DslSyntaxException: Concept with same key is described twice with different values."<br>
**Solution**: Please check if you have deployed both *SimpleWindowsAuth* package and *AspNetFormsAuth* package at the same time. Only one of the packages can be deployed on Rhetos server. Read the [installation](#installation) instructions above for more information on the issue.

**Issue**: Web service responds with error message "The Role Manager feature has not been enabled."<br>
**Solution**: The error occurs when the necessary modifications of Web.config file are not done. Please check that you have followed the [installation](#installation) instructions above.

**Issue**: I have accidentally deleted the *admin* user, *SecurityAdministrator* role, or some of its permissions. How can I get it back?<br>
**Solution**: Execute `AdminSetup.exe` again. It will regenerate the default administration settings. See [Admin user password](#admin-user-password).

**Other:** In case of a server error, additional information on the error may be found in the Rhetos app's log file (`RhetosServer.log` for Rhetos v1-v4).
If needed, more verbose logging of the authentication service may be switched on by enabling `Trace` level logger `AspNetFormsAuth.AuthenticationService`.
For example, in Rhetos v1-v4 add `<logger name="AspNetFormsAuth.AuthenticationService" minLevel="Trace" writeTo="TraceLog" />` in Rhetos application's `web.config`,
then the trace log will be written to `RhetosServerTrace.log`.

## Build

**Note:** This package is already available at the [NuGet.org](https://www.nuget.org/) online gallery.
You don't need to build it from source in order to use it in your application.

To build the package from source, run `Build.bat`.
The script will pause in case of an error.
The build output is a NuGet package in the "Install" subfolder.
