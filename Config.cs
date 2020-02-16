using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace PollBot {

    public partial class Config {

        [Required, YamlMember(Alias = "token")]
        public string TelegramToken { get; set; } = "";

        [YamlMember(Alias = "debug-mode")]
        public bool DebugMode { get; set; } = false;

        [Required, YamlMember(Alias = "main-id")]
        public long MainChatId { get; set; } = 0;

        [Required, YamlMember(Alias = "send-id")]
        public long SendChatId { get; set; } = 0;

        [Required, YamlMember(Alias = "database")]
        public string Database { get; set; } = "";

        [Required, YamlMember(Alias = "delete-origin")]
        public bool DeleteOrigin { get; set; } = false;

        [Required, YamlMember(Alias = "admin-direct-send")]
        public bool DirectSend { get; set; } = true;

        [Required, YamlMember(Alias = "admins")]
        public IEnumerable<long> Admins { get; set; }

        [Required, YamlMember(Alias = "texts")]
        public Translation translation;
    }
}