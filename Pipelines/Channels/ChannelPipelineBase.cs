namespace Pipelines.Channels
{
    public abstract class ChannelPipelineBase: IChannelPipeline
    {
        public void Execute()
        {
            Generator();
            Stage();
            Sink();
        }

        public void Generator()
        {
            throw new System.NotImplementedException();
        }

        public void Stage()
        {
            throw new System.NotImplementedException();
        }

        public void Sink()
        {
            throw new System.NotImplementedException();
        }
    }
}