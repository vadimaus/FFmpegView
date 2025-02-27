using ManagedBass;

namespace FFmpegView.Bass
{
    public unsafe class BassAudioStreamDecoder : AudioStreamDecoder
    {
        private Errors error;
        private int decodeStream;
        private bool isPausedOrStopped;
        public Errors LastError => error;


        public override void PauseCore()
        {
            isPausedOrStopped = ManagedBass.Bass.ChannelPause(decodeStream);
        }

        public override void StopCore()
        {
            isPausedOrStopped = ManagedBass.Bass.ChannelStop(decodeStream);
        }

        public override void Prepare()
        {
            if (decodeStream != 0)
                ManagedBass.Bass.StreamFree(decodeStream);

            decodeStream = ManagedBass.Bass.CreateStream(SampleRate, Channels, BassFlags.Mono, StreamProcedureType.Push);
            if (!ManagedBass.Bass.ChannelPlay(decodeStream, true))
                error = ManagedBass.Bass.LastError;
        }

        public override void PlayNextFrame(byte[] bytes)
        {
            if (isPausedOrStopped)
            {
                ManagedBass.Bass.ChannelPlay(decodeStream);
                isPausedOrStopped = false;
            }

            if (ManagedBass.Bass.StreamPutData(decodeStream, bytes, bytes.Length) == -1)
                error = ManagedBass.Bass.LastError;
        }
    }
}