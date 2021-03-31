namespace industrialization.Installation.BeltConveyor
{
    public interface IBeltConveyor
    {
        public BeltConveyorState GetState();

        void FlowItem();
    }
}