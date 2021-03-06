﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lens
{
    /// <summary>
    /// An immutable record-like data structure with validation.
    /// </summary>
    public interface IState : IRecord
    {
        bool IsValid();
    }
}
