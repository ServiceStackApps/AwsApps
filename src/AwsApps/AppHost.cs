using System.Collections.Generic;
using System.IO;
using Amazon;
using Amazon.S3;
using Funq;
using ServiceStack;
using ServiceStack.Auth;
using ServiceStack.Authentication.OAuth2;
using ServiceStack.Authentication.OpenId;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.Aws.S3;
using ServiceStack.Caching;
using ServiceStack.Configuration;
using ServiceStack.IO;
using ServiceStack.Razor;
using ServiceStack.Text;
using Todos;

namespace AwsApps
{
    public class AppHost : AppHostBase
    {
        public AppHost() : base("AWS Examples", typeof (AppHost).Assembly)
        {
            var customSettings = new FileInfo(@"~/appsettings.txt".MapHostAbsolutePath());
            AppSettings = customSettings.Exists
                ? (IAppSettings)new TextFileSettings(customSettings.FullName)
                : new AppSettings();
        }

        public override void Configure(Container container)
        {
            JsConfig.EmitCamelCaseNames = true;

            SetConfig(new HostConfig());

            Plugins.Add(new RazorFormat());

            //Comment out 2 lines below to change to use local FileSystem instead of S3
            var s3Client = new AmazonS3Client(AwsConfig.AwsAccessKey, AwsConfig.AwsSecretKey, RegionEndpoint.USEast1);
            VirtualFiles = new S3VirtualPathProvider(s3Client, AwsConfig.S3BucketName, this);

            container.Register<IPocoDynamo>(c => new PocoDynamo(AwsConfig.CreateAmazonDynamoDb()));
            var db = container.Resolve<IPocoDynamo>();
            db.RegisterTable<Todo>();
            db.InitSchema();

            //AWS Auth
            container.Register<ICacheClient>(new DynamoDbCacheClient(db));
            container.Register<IAuthRepository>(new DynamoDbAuthRepository(db));
            container.Resolve<IAuthRepository>().InitSchema();
            Plugins.Add(CreateAuthFeature());
        }

        public override List<IVirtualPathProvider> GetVirtualFileSources()
        {
            var fileSources = base.GetVirtualFileSources();
            fileSources.Add(VirtualFiles);
            return fileSources;
        }

        public AuthFeature CreateAuthFeature()
        {
            return new AuthFeature(() => new AuthUserSession(),
                new IAuthProvider[]
                {
                    new CredentialsAuthProvider(),              //HTML Form post of UserName/Password credentials
                    new BasicAuthProvider(),                    //Sign-in with HTTP Basic Auth
                    new DigestAuthProvider(AppSettings),        //Sign-in with HTTP Digest Auth
                    new TwitterAuthProvider(AppSettings),       //Sign-in with Twitter
                    new FacebookAuthProvider(AppSettings),      //Sign-in with Facebook
                    new YahooOpenIdOAuthProvider(AppSettings),  //Sign-in with Yahoo OpenId
                    new OpenIdOAuthProvider(AppSettings),       //Sign-in with Custom OpenId
                    new GoogleOAuth2Provider(AppSettings),      //Sign-in with Google OAuth2 Provider
                    new LinkedInOAuth2Provider(AppSettings),    //Sign-in with LinkedIn OAuth2 Provider
                    new GithubAuthProvider(AppSettings),        //Sign-in with GitHub OAuth Provider
                })
            {
                HtmlRedirect = "/awsauth/",
                IncludeRegistrationService = true,
            };
        }
    }
}