using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.IdentityModel.Tokens;

namespace SampleProject.Filters
{
    // Action filter: Cloudflare Origin Access token check  
    public class OriginCheckFilter : ActionFilterAttribute
    {
        readonly string _teamDomain;
        readonly string _publicCertsLocation;
        readonly TokenValidationParameters _tokenValidationParameters;

        static readonly string TeamDomainFormat = "https://{0}.cloudflareaccess.com";
        static readonly string PublicCertsFormat = "{0}/cdn-cgi/access/certs";
        static readonly string CloudflareOriginTokenHeaderName = "Cf-Access-Jwt-Assertion";
        public OriginCheckFilter(IConfiguration configuration)
        {
            var teamName = configuration["CloudflareTeamName"];
            var policyAUD = configuration["CloudflareAudience"];

            if(String.IsNullOrEmpty(teamName) || String.IsNullOrEmpty(policyAUD))
            {
                throw new ArgumentException("Cloudflare configuration is missing");
            }

            var _certsURL = $"{_teamDomain}/cdn-cgi/access/certs";
            _teamDomain = String.Format(TeamDomainFormat, teamName);
            _publicCertsLocation = string.Format(PublicCertsFormat, _teamDomain);

            _tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _teamDomain,
                ValidateAudience = true,
                ValidAudience = policyAUD,
                ValidateLifetime = true,
                IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) =>
                {
                    var client = new HttpClient();
                    var keySet = client.GetStringAsync(_publicCertsLocation).Result;
                    return new JsonWebKeySet(keySet).GetSigningKeys();
                }
            };
        }


        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var requestHeaders = context.HttpContext.Request.Headers;
            var cfOriginToken = requestHeaders[CloudflareOriginTokenHeaderName].FirstOrDefault();

            if (string.IsNullOrEmpty(cfOriginToken))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                tokenHandler.ValidateToken(cfOriginToken, _tokenValidationParameters, out _);
            }
            catch (Exception ex)
            {
                context.Result = new UnauthorizedObjectResult($"Invalid token: {ex.Message}");
                return;
            }

            base.OnActionExecuting(context);
        }
    }
}