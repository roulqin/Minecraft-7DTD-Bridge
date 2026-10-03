using System;
using System.Threading;
using MC7DTD;
class Program
{
    static void Main(string[] args)
    {
        using (var client = new BridgeClient(args[0], Console.WriteLine))
        { client.Start(); Thread.Sleep(int.Parse(args[1]) * 1000); }
    }
}
