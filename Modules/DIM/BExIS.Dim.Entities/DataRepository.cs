﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BExIS.Dim.Entities
{
    public class DataRepository
    {
        public string Name { get; set; }
        public string ReqiuredMetadataStandard { get; set; }
        public string PrimaryDataFormat { get; set; }
        public string Server { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
    }
}
