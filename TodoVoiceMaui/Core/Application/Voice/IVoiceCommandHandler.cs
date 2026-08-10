using System.Threading;
using System.Threading.Tasks;

namespace TodoVoiceMaui.Core.Application.Voice;

public interface IVoiceCommandHandler
{
    Task<VoiceCommandResult> HandleAsync(VoiceCommand command, CancellationToken ct = default);
}
