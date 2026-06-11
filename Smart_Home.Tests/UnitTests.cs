using Smart_Home.Service;
using Xunit;

namespace Smart_Home.Tests
{
    public class MqttTopicsTests
    {
        [Fact]
        public void DeviceCommand_ConvertsDotsToSlashes_AndAppendsSet()
        {
            Assert.Equal("smarthome/home/light/living_room/set",
                MqttTopics.DeviceCommand("home.light.living_room"));
        }

        [Fact]
        public void StatusOnline_BuildsRetainedPresenceTopic()
        {
            Assert.Equal("smarthome/status/esp32-door/online", MqttTopics.StatusOnline("esp32-door"));
        }

        [Fact]
        public void Status_BuildsNodeStatusTopic()
        {
            Assert.Equal("smarthome/status/esp32-home", MqttTopics.Status("esp32-home"));
        }

        [Fact]
        public void Constants_UseSmarthomePrefix()
        {
            Assert.Equal("smarthome", MqttTopics.Prefix);
            Assert.StartsWith("smarthome/", MqttTopics.DoorControl);
            Assert.StartsWith("smarthome/", MqttTopics.SensorHome);
            Assert.StartsWith("smarthome/", MqttTopics.DoorBreach);
        }
    }

    public class OperationResultTests
    {
        [Fact]
        public void Ok_IsSuccessWithoutError()
        {
            var result = OperationResult.Ok();
            Assert.True(result.Success);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public void Fail_CarriesMessage()
        {
            var result = OperationResult.Fail("boom");
            Assert.False(result.Success);
            Assert.Equal("boom", result.ErrorMessage);
        }
    }

    public class BcryptPasswordHasherTests
    {
        [Fact]
        public void HashThenVerify_RoundTrips()
        {
            var hasher = new BcryptPasswordHasher();
            var hash = hasher.Hash("1234");

            Assert.NotEqual("1234", hash);          // never stores plaintext
            Assert.True(hasher.Verify("1234", hash));
            Assert.False(hasher.Verify("9999", hash));
        }
    }
}
