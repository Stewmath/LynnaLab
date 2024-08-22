using System;
using LynnaLib;

namespace LynnaLab
{
    public interface IBackend
    {
        /// <summary>
        /// Called before ImGui rendering occurs
        /// </summary>
        public void HandleEvents(float deltaTime);

        /// <summary>
        /// Called after main imgui rendering occurs, this will draw the results of that
        /// </summary>
        public void Render();

        public Image ImageFromBitmap(Bitmap bitmap);
    }
}
