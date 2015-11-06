using System.Net.Mail;
using System.Net;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using ServiceStack;
using ServiceStack.FluentValidation;
using ServiceStack.OrmLite;
using ServiceStack.Aws;
using ServiceStack.Aws.DynamoDb;

namespace EmailContacts
{
    public class CotntactsValidator : AbstractValidator<CreateContact>
    {
        public CotntactsValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("A Name is what's needed.");
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.Age).GreaterThan(0);
        }
    }

    public class ContactsServices : Service
    {
        public IPocoDynamo Dynamo { get; set; }

        public Contact Any(GetContact request)
        {
            return Dynamo.GetItem<Contact>(request.Id);
        }

        public List<Contact> Any(FindContacts request)
        {
            return request.Age.HasValue
                ? Dynamo.FromQueryIndex<ContactAgeIndex>(q => q.Age == request.Age.Value).ExecInto<Contact>().ToList()
                : Dynamo.ScanAll<Contact>().ToList();
        }

        public Contact Post(CreateContact request)
        {
            var contact = request.ConvertTo<Contact>();
            Dynamo.PutItem(contact);
            return contact;
        }

        public void Any(DeleteContact request)
        {
            Dynamo.DeleteItem<Contact>(request.Id);
        }

        public void Any(Reset request)
        {
            Dynamo.DeleteItems<Email>(Dynamo.FromScan<Email>().ExecColumn(x => x.Id));
            Dynamo.DeleteItems<Contact>(Dynamo.FromScan<Contact>().ExecColumn(x => x.Id));
            AddCustomers(Dynamo);
        }

        public static void AddCustomers(IPocoDynamo db)
        {
            db.PutItem(new Contact { Name = "Kurt Cobain", Email = "demo+kurt@servicestack.net", Age = 27 });
            db.PutItem(new Contact { Name = "Jimi Hendrix", Email = "demo+jimi@servicestack.net", Age = 27 });
            db.PutItem(new Contact { Name = "Michael Jackson", Email = "demo+mike@servicestack.net", Age = 50 });
        }
    }

    public class EmailContactValidator : AbstractValidator<EmailContact>
    {
        public EmailContactValidator()
        {
            RuleFor(x => x.Subject).NotEmpty();
        }
    }

    public class EmailServices : Service
    {
        public IEmailer Emailer { get; set; }

        public IPocoDynamo Dynamo { get; set; }

        public EmailContactResponse Any(EmailContact request)
        {
            var contact = Dynamo.GetItem<Contact>(request.ContactId);
            if (contact == null)
                throw HttpError.NotFound("Contact does not exist");

            var msg = new Email { From = "demo@servicestack.net", To = contact.Email }.PopulateWith(request);
            Emailer.Email(msg);

            return new EmailContactResponse { Email = contact.Email };
        }

        public object Any(FindEmails request)
        {
            var query = request.To != null
                ? Dynamo.FromScan<Email>(q => q.To == request.To)
                : Dynamo.FromScan<Email>();

            return query.Exec(limit: request.Take.GetValueOrDefault(10));
        }
    }

    public class SmtpConfig
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
    }

    public interface IEmailer
    {
        void Email(Email email);
    }

    public class SmtpEmailer : IEmailer
    {
        public SmtpConfig Config { get; set; }

        public IPocoDynamo Dynamo { get; set; }

        public void Email(Email email)
        {
            var msg = new MailMessage(email.From, email.To).PopulateWith(email);
            using (var client = new SmtpClient(Config.Host, Config.Port))
            {
                client.Credentials = new NetworkCredential(Config.UserName, Config.Password);
                client.EnableSsl = true;
                client.Send(msg);
            }

            Dynamo.PutItem(email);
        }
    }

    public class DbEmailer : IEmailer
    {
        public IPocoDynamo Dynamo { get; set; }

        public void Email(Email email)
        {
            Thread.Sleep(1000);  //simulate processing delay
            Dynamo.PutItem(email);
        }
    }
}