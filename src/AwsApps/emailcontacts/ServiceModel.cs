using System.Collections.Generic;
using ServiceStack;
using ServiceStack.DataAnnotations;
using ServiceStack.Aws.DynamoDb;

namespace EmailContacts
{
    [Route("/emails")]
    public class FindEmails
    {
        public int? Take { get; set; }
        public string To { get; set; }
    }

    [Route("/contacts", "GET")]
    public class FindContacts : IReturn<List<Contact>>
    {
        public int? Age { get; set; }
    }

    [Route("/contacts/{Id}", "GET")]
    public class GetContact : IReturn<Contact>
    {
        public int Id { get; set; }
    }

    [Route("/contacts", "POST")]
    public class CreateContact
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public int Age { get; set; }
    }

    [Route("/contacts/email", "POST")]
    public class EmailContact : IReturn<EmailContactResponse>
    {
        public int ContactId { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
    }

    public class EmailContactResponse
    {
        public string Email { get; set; }

        public ResponseStatus ResponseStatus { get; set; }
    }

    [Route("/contacts/{Id}/delete")]
    public class DeleteContact
    {
        public int Id { get; set; }
    }

    [Route("/emailcontacts/reset")]
    public class Reset {}

    public class Email
    {
        [AutoIncrement]
        public int Id { get; set; }
        public string To { get; set; }
        public string From { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
    }

    [References(typeof(ContactAgeIndex))]
    public class Contact
    {
        [AutoIncrement]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public int Age { get; set; }
    }

    public class ContactAgeIndex : IGlobalIndex<Contact>
    {
        [HashKey]
        public int Age { get; set; }

        [RangeKey]
        public int Id { get; set; }

        public string FirstName { get; set; }
        public string LastName { get; set; }
        public bool Alive { get; set; }
    }
}