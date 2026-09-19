using System;
using System.Collections.Generic;
using System.Text;

namespace SchoolManagement.Application.ClassTeachers.DTOs
{
    public class ReviewTemporaryClassAccessRequest
    {
        public bool Approve { get; set; }

        public string? Remarks { get; set; }
    }
}
