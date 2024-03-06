namespace Catan3.Models
{
    public partial class PersonModel
    {
        public string FullName => FirstName + " " + LastName;
    }
}
