namespace Pipelines.Channels
{
    public interface IChannelPipeline: IPipeline
    {
        void Generator();
        void Stage();
        void Sink();
    }
}