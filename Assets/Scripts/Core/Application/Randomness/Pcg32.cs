// PCG-XSH-RR 64/32의 상태 전이·초기화·편향 없는 정수 범위 추첨을 구현합니다.
// Core 전용 값 타입이며 Unity나 전역 난수 상태에 의존하지 않습니다.
// PCG minimal C (c) 2014 M.E. O'Neill, Apache-2.0; C# port by TeamHJD.
// 원본과 달리 상태를 값으로 복사하며 사용 권한과 수명은 RandomService가 검사합니다.
// 출처와 라이선스: Docs/licenses/pcg32-LICENSE.txt

namespace TeamHJD.Game.Application
{
    internal struct Pcg32
    {
        public ulong State;
        private readonly ulong _increment;

        public Pcg32(ulong seed, ulong sequence)
        {
            State = 0;
            _increment = unchecked((sequence << 1) | 1);
            Next();
            State = unchecked(State + seed);
            Next();
        }

        public uint Next()
        {
            ulong previous = State;
            State = unchecked(previous * 6364136223846793005UL + _increment);
            uint mixed = (uint)(((previous >> 18) ^ previous) >> 27);
            int rotation = (int)(previous >> 59);
            return (mixed >> rotation) | (mixed << ((-rotation) & 31));
        }

        public uint Bounded(uint bound)
        {
            uint threshold = unchecked(0u - bound) % bound;
            uint value;
            do
            {
                value = Next();
            } while (value < threshold);
            return value % bound;
        }
    }
}
