namespace Grace.VWaterOriginal.Editor
{
    /// <summary>Keeps pending numeric edits while synchronizing changes to evaluated asset values.</summary>
    public sealed class DownwashCurveDraft
    {
        private bool initialized;
        private int profileId;
        private double height;
        private DownwashResponseValues evaluated;
        public double[] Values { get; private set; } = new double[6];

        public void Sync(int newProfileId,double newHeight,DownwashResponseValues current,bool force)
        {
            bool refresh=force || !initialized || profileId!=newProfileId || height!=newHeight || !evaluated.Equals(current);
            initialized=true;profileId=newProfileId;height=newHeight;evaluated=current;
            if(refresh)Values=new[]{current.MistAmount,current.SprayHeight,current.WaveHeight,current.Pressure,current.Foam,current.MistTurbulence};
        }
        public void Invalidate(){initialized=false;}
    }
}
