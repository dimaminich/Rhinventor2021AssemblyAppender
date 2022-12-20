﻿using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace Rhinventor2021AssemblyAppender
{
    public class Rhinventor2021AssemblyAppenderInfo : GH_AssemblyInfo
    {
        public override string Name
        {
            get
            {
                return "Rhinventor2021AssemblyAppender";
            }
        }
        public override Bitmap Icon
        {
            get
            {
                //Return a 24x24 pixel bitmap to represent this GHA library.
                return null;
            }
        }
        public override string Description
        {
            get
            {
                //Return a short string describing the purpose of this GHA library.
                return "";
            }
        }
        public override Guid Id
        {
            get
            {
                return new Guid("78b144af-8e4c-44e2-ba48-015bd2f99e23");
            }
        }

        public override string AuthorName
        {
            get
            {
                //Return a string identifying you or your company.
                return "";
            }
        }
        public override string AuthorContact
        {
            get
            {
                //Return a string representing your preferred contact details.
                return "";
            }
        }
    }
}
