using Xenko.Engine;

namespace WindowsMixedRealityDemo
{
    class WindowsMixedRealityDemoApp
    {
        static void Main(string[] args)
        {
            using (var game = new Game())
            {
                game.Run();
            }
        }
    }
}
