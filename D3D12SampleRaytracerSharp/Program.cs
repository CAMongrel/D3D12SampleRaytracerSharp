// Copyright (c) Henning Thoele.
// Distributed under the MIT license. See the LICENSE file in the project root for more information.

using System;

namespace D3D12SampleRaytracerSharp
{
    class Program
    {
        private class TestApplication : Application
        {
            public TestApplication()
            {
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            using (var app = new TestApplication())
            {
                app.Run();
            }
        }
    }
}
