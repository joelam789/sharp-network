using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpNetwork.SimpleProtocol
{
    public interface IHandler
    {
        Object Handle(Object data);
    } 
}
