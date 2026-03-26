namespace RecordClient.Models.Device
{
    public class DeviceItem
    {
        public string Name { get; set; } = "";
        public string ID { get; set; } = "";

        public override string ToString() => Name;
    }
}
