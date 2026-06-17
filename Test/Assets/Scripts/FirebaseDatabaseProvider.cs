using Firebase.Database;
using System;
using System.Collections.Generic;
using System.Reflection;

public static class FirebaseDatabaseProvider
{
    static readonly Dictionary<string, FirebaseDatabase> databases = new Dictionary<string, FirebaseDatabase>();
    static readonly HashSet<string> configuredUrls = new HashSet<string>();

    public static DatabaseReference GetRootReference(string databaseUrl)
    {
        return GetDatabase(databaseUrl).RootReference;
    }

    public static FirebaseDatabase GetDatabase(string databaseUrl)
    {
        if (databases.ContainsKey(databaseUrl))
        {
            return databases[databaseUrl];
        }

        FirebaseDatabase database = FirebaseDatabase.GetInstance(databaseUrl);
        DisablePersistence(database, databaseUrl);
        databases[databaseUrl] = database;
        return database;
    }

    static void DisablePersistence(FirebaseDatabase database, string databaseUrl)
    {
        if (configuredUrls.Contains(databaseUrl))
        {
            return;
        }

        configuredUrls.Add(databaseUrl);

        try
        {
            MethodInfo method = typeof(FirebaseDatabase).GetMethod("SetPersistenceEnabled", new[] { typeof(bool) });
            if (method != null)
            {
                method.Invoke(database, new object[] { false });
            }
        }
        catch (Exception)
        {
            // The database is still usable if persistence has already been configured by the SDK.
        }
    }
}
