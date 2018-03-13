using System;

namespace SharpNetwork.SimpleProtocol
{
    public interface IHandler
    {
        Object Handle(Object data);
    } 
}
