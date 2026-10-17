// 특정 Match 시작 설정에 대한 준비 응답을 구분합니다.
namespace TeamHJD.Game.Application
{
    public enum MatchReadinessStatus { Accepted, AlreadyReady, UnknownConnection, DifferentMatch, DifferentDescriptor, AlreadyStarted }
}
