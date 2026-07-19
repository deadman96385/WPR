namespace System.Device.Location
{
    public class GeoCoordinateWatcher : IDisposable
    {
        private GeoPositionAccuracy _Accuracy;
        private double _Threshold;
        private bool _Disposed;

        public event EventHandler<GeoLocationChangedEventArgs> LocationChanged;
        public event EventHandler<GeoPositionChangedEventArgs<GeoCoordinate>> PositionChanged;
        public event EventHandler<GeoPositionStatusChangedEventArgs> StatusChanged;

        public GeoCoordinateWatcher(GeoPositionAccuracy accuracy)
        {
            _Accuracy = accuracy;
        }

        public double MovementThreshold
        {
            get => _Threshold;
            set {
                if (value < 0.0 || Double.IsNaN(value))
                {
                    throw new ArgumentOutOfRangeException("Threshold value is set to negative!");
                }

                _Threshold = value;
            }
        }

        public GeoPositionAccuracy DesiredAccuracy => _Accuracy;

        // The desktop host has no location provider, so the watcher reports the
        // pair the phone reports when the location service is off rather than a
        // state that invites a title to keep waiting. Initializing would say a
        // fix is coming and NoData would say the sensors are there but silent;
        // neither is true here, and both leave a title polling for a fix that
        // cannot arrive. Disabled with Denied is a state every title that uses
        // location already handles, and it is the honest one.
        public GeoPositionStatus Status => GeoPositionStatus.Disabled;

        public GeoPositionPermission Permission => GeoPositionPermission.Denied;

        // Never null: titles read Position.Location straight through, and the
        // phone hands back an unknown coordinate rather than nothing at all.
        public GeoPosition<GeoCoordinate> Position { get; } =
            new GeoPosition<GeoCoordinate>(DateTimeOffset.MinValue, GeoCoordinate.Unknown);

        public void Start()
        {

        }

        public void Stop()
        {

        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed)
            {
                return;
            }

            if (disposing)
            {
                Stop();
            }

            _Disposed = true;
        }
    }
}