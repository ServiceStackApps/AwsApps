using ServiceStack;
using System.Collections.Generic;
using System.Runtime.Serialization;
using ServiceStack.Auth;
using ServiceStack.Aws.DynamoDb;

namespace AwsAuth
{
    [Route("/awsauth/session")]
    public class GetSession : IReturn<GetSessionResponse> {}

    public class GetSessionResponse
    {
        public AuthUserSession Result { get; set; }
        public ResponseStatus ResponseStatus { get; set; }
    }

    [Route("/awsauth/reset")]
    public class Reset { }

    public class AwsAuthService : Service
    {
        public object Any(GetSession request)
        {
            return new GetSessionResponse {
                Result = SessionAs<AuthUserSession>(),
            };
        }

        public object Any(Reset request)
        {
            ResetUsers((DynamoDbAuthRepository)TryResolve<IAuthRepository>());
            return "OK";
        }

        public static void ResetUsers(DynamoDbAuthRepository authRepo)
        {
            authRepo.DeleteUserAuth(1);
            authRepo.DeleteUserAuth(2);

            CreateUser(authRepo, 1, "test", "test", new List<string> { "TheRole" }, new List<string> { "ThePermission" });
            CreateUser(authRepo, 2, "test2", "test");
        }

        private static void CreateUser(IUserAuthRepository authRepo,
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
}