//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Copyright (c) 2008 - 2015 Jb Evain
// Copyright (c) 2008 - 2011 Novell, Inc.
//
// Licensed under the MIT/X11 license.
//

namespace MonoFN.Cecil
{
    public sealed class LinkedResource : Resource
    {
        internal byte[] hash;
        public byte[] Hash
        {
            get { return hash; }
        }
        public string File { get; set; }
        public override ResourceType ResourceType
        {
            get { return ResourceType.Linked; }
        }
        public LinkedResource(string name, ManifestResourceAttributes flags) : base(name, flags) { }

        public LinkedResource(string name, ManifestResourceAttributes flags, string file) : base(name, flags)
        {
            this.File = file;
        }
    }
}