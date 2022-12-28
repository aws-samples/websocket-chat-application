using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AWS.Lambda.Powertools.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Shared;

public class CognitoJwtVerifier
{
    private readonly string _userPoolId;
    private readonly string _clientId;
    private readonly string _region;

    public CognitoJwtVerifier(string userPoolId, string clientId, string region)
    {
        _userPoolId = userPoolId;
        _clientId = clientId;
        _region = region;
    }
    [Logging(LogEvent = true, Service = "websocketMessagingService")]
    public JwtSecurityToken? VerifyToken(string token)
    {
        var jwtSecurityTokenHandler = new JwtSecurityTokenHandler();
        if (!jwtSecurityTokenHandler.CanReadToken(token)) return null;
        
        var tokenValidationParameters = new TokenValidationParameters
        {
            IssuerSigningKeyResolver = (s, securityToken, identifier, parameters) =>
            {
                //TODO: Webclient is Obsolete, use HttpClient instead (but keep it simple)
                var json = new WebClient().DownloadString(parameters.ValidIssuer + "/.well-known/jwks.json");
                var keys = JsonWebKeySet.Create(json);
                return (IEnumerable<SecurityKey>)keys.GetSigningKeys();
            },
            ValidIssuer = $"https://cognito-idp.{_region}.amazonaws.com/{_userPoolId}",
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidateLifetime = true,
            ValidAudience = _clientId,
            ValidateAudience = true
        };
        
        try
        {
            jwtSecurityTokenHandler.ValidateToken(token, tokenValidationParameters, out var jwtValidatedToken);
        }
        catch (Exception e)
        {
            Logger.LogError(e);
            return null;
        }

        return new JwtSecurityToken(jwtEncodedString: token);
    }
}