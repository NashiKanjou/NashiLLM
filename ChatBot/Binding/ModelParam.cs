/*
 *  This file is part of ArsCore.
 *
 *  ArsCore is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  ArsCore is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with ArsCore.  If not, see <https://www.gnu.org/licenses/>.
 */
namespace ChatBot.Binding
{
    public class ModelParam
    {
        public int MainGpu { get; set; }
        public int GpuLayerCount { get; set; }
        public int ContextSize { get; set; }
        public int MaxTokens { get; set; }
        public uint BatchSize { get; set; }
        public float Temperature { get; set; }
        public float RepeatPenalty { get; set; }
        public int Workers { get; set; }
        public bool UseStreaming { get; set; }
        public bool FlashAttention { get; set; }
        public bool UseMemoryMap { get; set; }
        public string FileToLoad { get; set; }
        public string ModelName { get; set; }
        public float TopP { get; set; }
    }
}
