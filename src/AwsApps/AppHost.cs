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
using ServiceStack.Aws.Sqs;
using ServiceStack.Caching;
using ServiceStack.Configuration;
using ServiceStack.IO;
using ServiceStack.Messaging;
using ServiceStack.Razor;
using ServiceStack.Text;

namespace AwsApps
{
    public class AppHost : AppHostBase
    {
        public AppHost() : base("AWS Examples", typeof(AppHost).Assembly)
        {
#if !DEBUG  //App deployed with RELEASE version which uses Config settings in DynamoDb
            AppSettings = new MultiAppSettings(
                new DynamoDbAppSettings(new PocoDynamo(AwsConfig.CreateAmazonDynamoDb()), initSchema:true),
                new AppSettings());
#endif
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
            db.RegisterTable<Todos.Todo>();
            db.RegisterTable<EmailContacts.Email>();
            db.RegisterTable<EmailContacts.Contact>();
            db.InitSchema();

            //AWS Auth
            container.Register<ICacheClient>(new DynamoDbCacheClient(db, initSchema:true));
            container.Register<IAuthRepository>(new DynamoDbAuthRepository(db, initSchema:true));
            Plugins.Add(CreateAuthFeature());

            //EmailContacts
            ConfigureSqsMqServer(container);
            ConfigureEmailer(container);
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

        private void ConfigureSqsMqServer(Container container)
        {
            container.Register<IMessageService>(c => new SqsMqServer(
                AwsConfig.AwsAccessKey, AwsConfig.AwsSecretKey, RegionEndpoint.USEast1));

            var mqServer = container.Resolve<IMessageService>();
            mqServer.RegisterHandler<EmailContacts.EmailContact>(ServiceController.ExecuteMessage);
            mqServer.Start();
        }

        private void ConfigureEmailer(Container container)
        {
            //If SmtpConfig exists, use real SMTP Emailer else use simulated DbEmailer
            var smtpConfig = AppSettings.Get<EmailContacts.SmtpConfig>("SmtpConfig");
            if (smtpConfig != null)
            {
                container.Register(smtpConfig);
                container.RegisterAs<EmailContacts.SmtpEmailer, EmailContacts.IEmailer>().ReusedWithin(ReuseScope.Request);
            }
            else
            {
                container.RegisterAs<EmailContacts.DbEmailer, EmailContacts.IEmailer>().ReusedWithin(ReuseScope.Request);
            }
        }
    }

    [Route("/config")]
    public class GetAppConfig {}

    public class AppServices : Service
    {
        public object Any(GetAppConfig request)
        {
            return HostContext.AppSettings.GetAll();
        }
    }
}