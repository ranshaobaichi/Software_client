namespace Automation.Protocol {
    public enum MapRequestType {
        MAP_INIT = 0,
        MAP_MOVE = 1,
    }

    public enum MapResponseType {
        MAP_INIT = 0,
        MAP_SYNC = 1,
    }

    public enum MapPushMessageType {
        MAP_SYNC = 1,
    }
}
