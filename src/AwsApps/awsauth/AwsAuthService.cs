using ServiceStack;
using System.Collections.Generic;
using ServiceStack.Auth;

namespace AwsAuth
{
    [Route("/awsauth/userinfo")]
    public class GetUserInfo : IReturn<GetUserInfoResponse> {}
    public class GetUserInfoResponse
    {
        public AuthUserSession Session { get; set; }
        public UserAuth UserAuth { get; set; }
        public List<UserAuthDetails> UserAuthDetails { get; set; }
        public ResponseStatus ResponseStatus { get; set; }
    }

    [Route("/awsauth/reset")]
    public class Reset {}

    public class AwsAuthService : Service
    {
        public IAuthRepository AuthRepo { get; set; }

        public object Any(GetUserInfo request)
        {
            var session = SessionAs<AuthUserSession>();
            if (!session.IsAuthenticated)
                throw HttpError.Unauthorized("Requires Authentication");

            return new GetUserInfoResponse {
                Session = session,
                UserAuth = (UserAuth)AuthRepo.GetUserAuth(session.UserAuthId),
                UserAuthDetails = AuthRepo.GetUserAuthDetails(session.UserAuthId).Map(x => (UserAuthDetails)x),
            };
        }

        public object Any(Reset request)
        {
            ((IClearable)AuthRepo).Clear();

            CreateUser(AuthRepo, 1, "test", "test", new List<string> { "TheRole" }, new List<string> { "ThePermission" });
            CreateUser(AuthRepo, 2, "test2", "test");

            base.Request.RemoveSession();

            return HttpResult.Redirect("/awsauth/#s=1");
        }

        private static void CreateUser(IAuthRepository authRepo,
            int id, string username, string password, List<string> roles = null, List<string> permissions = null)
        {
            string hash;
            string salt;
            new SaltedHash().GetHashAndSaltString(password, out hash, out salt);

            var userAuth = authRepo.CreateUserAuth(new UserAuth
            {
                Id = id,
                DisplayName = username + " DisplayName",
                Email = username + "@gmail.com",
                UserName = username,
                FirstName = "First " + username,
                LastName = "Last " + username,
                PasswordHash = hash,
                Salt = salt,
                Roles = roles,
                Permissions = permissions
            }, password);

            authRepo.AssignRoles(userAuth, roles, permissions);
        }
    }

    [Route("/awsauth/RequiresAuth")]
    public class RequiresAuth { }

    [Route("/awsauth/RequiresRole")]
    public class RequiresRole { }

    [AddHeader(ContentType = MimeTypes.Html)]
    public class RequiresAuthService : Service
    {
        [Authenticate]
        public object Any(RequiresAuth request)
        {
            return "<h1>HAS AUTH!</h1>";
        }

        [RequiredRole("TheRole")]
        public object Any(RequiresRole request)
        {
            return "<h1>HAS ROLE!</h1>";
        }
    }

}