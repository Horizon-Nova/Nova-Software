using NetToolTestArea.Model;
using NetToolTestArea.Utility;

namespace NetToolTestArea.Services
{
    public sealed class KeyToolService
    {
        private readonly KeyToolUtility keyToolUtility;

        public KeyToolService(KeyToolUtility keyToolUtility)
        {
            this.keyToolUtility = keyToolUtility;
        }

        public KeyToolSnapshot QueryLocalMachineFingerprints() => keyToolUtility.QueryLocalMachineFingerprints();

        public KeyToolSnapshot ComputeKeyFromValues(
            string moduleId,
            string guidKey,
            string cpuProcessorId,
            string smbiosUuid,
            string biosSerialNumber,
            string baseBoardSerialNumber,
            string systemDriveVolumeSerial,
            string physicalNicMac)
            => keyToolUtility.ComputeKeyFromValues(moduleId, guidKey, cpuProcessorId, smbiosUuid, biosSerialNumber, baseBoardSerialNumber, systemDriveVolumeSerial, physicalNicMac);

        public KeyToolSnapshot VerifyKeyAgainstValues(
            string moduleId,
            string licenseKey,
            string cpuProcessorId,
            string smbiosUuid,
            string biosSerialNumber,
            string baseBoardSerialNumber,
            string systemDriveVolumeSerial,
            string physicalNicMac)
            => keyToolUtility.VerifyKeyAgainstValues(moduleId, licenseKey, cpuProcessorId, smbiosUuid, biosSerialNumber, baseBoardSerialNumber, systemDriveVolumeSerial, physicalNicMac);
    }
}
