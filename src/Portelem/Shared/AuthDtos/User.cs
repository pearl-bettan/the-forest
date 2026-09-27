using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UsersManager.Shared
{
    // המשתמש המחובר
    public class User
    {
        //  המזהה של פורטלם
        public int Id { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        // הדוא"ל מפורטלם
        public string Email { get; set; }
    }



}
