using LibreHardwareMonitor.Hardware;

namespace HardwareScript
{
    public class HardwareManager
    {
        protected Computer _computer;

        protected Dictionary<string, ISensor> sensors = new Dictionary<string, ISensor>();

        public SortedDictionary<string, IElement> nameToElement = new SortedDictionary<string, IElement>();
        public Dictionary<IElement, string> elementToName = new Dictionary<IElement, string>();

        protected Dictionary<IHardware, int> watchedHardware = new Dictionary<IHardware, int>();

        public HardwareManager(Computer computer)
        {
            _computer = computer;
        }

        public void Open()
        {
            foreach (IHardware hardware in _computer.Hardware) {
                OnHardwareAdded(hardware);
            }

            _computer.HardwareAdded += OnHardwareAdded;
            _computer.HardwareRemoved += OnHardwareRemoved;
        }

        public void Close()
        {
            _computer.HardwareAdded -= OnHardwareAdded;
            _computer.HardwareRemoved -= OnHardwareRemoved;

            foreach (IHardware hardware in _computer.Hardware) {
                OnHardwareRemoved(hardware);
            }
        }

        protected void OnHardwareAdded(IHardware hardware)
        {
            string name;
            if (hardware.Parent == null) {
                name = hardware.Name;
            } else {
                name = $"{elementToName[hardware.Parent]} / {hardware.Name}";
            }
            nameToElement[name] = hardware;
            elementToName[hardware] = name;

            hardware.SensorAdded += OnSensorAdded;
            hardware.SensorRemoved += OnSensorRemoved;

            foreach (ISensor sensor in hardware.Sensors) {
                OnSensorAdded(sensor);
            }

            foreach (IHardware subHardware in hardware.SubHardware) {
                OnHardwareAdded(subHardware);
            }
        }

        protected void OnHardwareRemoved(IHardware hardware)
        {
            foreach (ISensor sensor in hardware.Sensors) {
                OnSensorRemoved(sensor);
            }

            foreach (IHardware subHardware in hardware.SubHardware) {
                OnHardwareRemoved(subHardware);
            }

            nameToElement.Remove(elementToName[hardware]);
            elementToName.Remove(hardware);

            // Console.WriteLine($"Removed hardware {hardware.Name}");
        }

        protected void OnSensorAdded(ISensor sensor)
        {
            string name = $"{elementToName[sensor.Hardware]} / {SensorTypeToString(sensor.SensorType)} / {sensor.Name}";
            nameToElement[name] = sensor;
            elementToName[sensor] = name;

            // Console.WriteLine($"Added sensor: {name}");
            // sensors[sensor.Identifier.ToString()] = sensor;
        }

        protected void OnSensorRemoved(ISensor sensor)
        {
            // Console.WriteLine($"Removed sensor: {elementToName[sensor]}");

            nameToElement.Remove(elementToName[sensor]);
            elementToName.Remove(sensor);
        }

        protected string SensorTypeToString(SensorType type)
        {
            switch (type) {
                case SensorType.Voltage:
                    return "Voltages";
                case SensorType.Current:
                    return "Currents";
                case SensorType.Energy:
                    return "Capacities";
                case SensorType.Clock:
                    return "Clocks";
                case SensorType.Load:
                    return "Load";
                case SensorType.Temperature:
                    return "Temperatures";
                case SensorType.Fan:
                    return "Fans";
                case SensorType.Flow:
                    return "Flows";
                case SensorType.Control:
                    return "Controls";
                case SensorType.Level:
                    return "Levels";
                case SensorType.Power:
                    return "Powers";
                case SensorType.Data:
                    return "Data";
                case SensorType.SmallData:
                    return "Data";
                case SensorType.Factor:
                    return "Factors";
                case SensorType.Frequency:
                    return "Frequencies";
                case SensorType.Throughput:
                    return "Throughput";
                case SensorType.TimeSpan:
                    return "Times";
            }

            throw new Exception();
        }
    }
}
