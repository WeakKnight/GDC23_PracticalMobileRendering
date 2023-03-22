namespace PMRP
{
    public class Singleton<T> where T : class, new()
    {
        private static T s_instance;

        protected Singleton()
        {

        }


        public static bool IsValid()
        {
            return s_instance != null;
        }

        public static void CreateInstance()
        {
            if (s_instance == null)
            {
                s_instance = new T();

                (s_instance as Singleton<T>).Init();
            }
        }

        public static void DestroyInstance()
        {
            if (s_instance != null)
            {
                (s_instance as Singleton<T>).UnInit();
                s_instance = null;
            }
        }

        public static T Instance
        {
            get
            {
                if (s_instance == null)
                {
                    CreateInstance();
                }

                return s_instance;
            }
        }

        public static T GetInstance()
        {
            if (s_instance == null)
            {
                CreateInstance();
            }

            return s_instance;
        }

        public static bool HasInstance()
        {
            return (s_instance != null);
        }

        protected virtual void Init()
        {

        }

        protected virtual void UnInit()
        {

        }
    }
}
