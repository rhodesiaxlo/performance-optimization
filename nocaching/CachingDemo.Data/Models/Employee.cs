﻿using System;

namespace CachingDemo.Data.Models
{
    public class Employee : Person
    {
        public Employee()
            : base()
        {
        }

        public string JobTitle { get; set; }

        public DateTime HireDate { get; set; }
    }
}
