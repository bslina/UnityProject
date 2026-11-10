using UnityEngine;

// Builds the roadside objects of one zone for ONE road segment (cacti, palms, lamps, buildings...).
// Everything is made from primitives, so it works without any asset or editor setup.
// Positions are local to the segment: x = across the road, z = along it (-len/2 .. +len/2).
public static class RoadDecor
{
    static readonly Color CactusGreen = new Color(0.20f, 0.50f, 0.22f);
    static readonly Color Trunk = new Color(0.45f, 0.30f, 0.18f);
    static readonly Color Leaf = new Color(0.14f, 0.55f, 0.22f);
    static readonly Color Coconut = new Color(0.30f, 0.20f, 0.10f);
    static readonly Color Metal = new Color(0.30f, 0.32f, 0.35f);
    static readonly Color LampLight = new Color(1f, 0.92f, 0.55f);
    static readonly Color Window = new Color(1f, 0.90f, 0.50f);
    static readonly Color Foam = new Color(0.88f, 0.96f, 1f);
    static readonly Color Sand = new Color(0.95f, 0.85f, 0.62f);

    static readonly Color[] Umbrellas =
    {
        new Color(0.90f, 0.15f, 0.15f), new Color(1f, 0.80f, 0.15f),
        new Color(0.15f, 0.45f, 0.90f), new Color(0.95f, 0.95f, 0.95f)
    };

    static readonly Color[] Buildings =
    {
        new Color(0.45f, 0.47f, 0.52f), new Color(0.62f, 0.58f, 0.55f),
        new Color(0.35f, 0.38f, 0.45f), new Color(0.70f, 0.55f, 0.50f)
    };

    static readonly Color[] Boards =
    {
        new Color(0.90f, 0.20f, 0.55f), new Color(0.20f, 0.60f, 0.90f), new Color(0.95f, 0.65f, 0.10f)
    };

    public static Transform Build(ZoneTheme theme, Transform segment, float len, float half)
    {
        var root = new GameObject("Decor").transform;
        root.SetParent(segment, false);

        switch (theme.decor)
        {
            case ZoneTheme.Decor.Beach: BuildBeach(root, len, half); break;
            case ZoneTheme.Decor.Hollywood: BuildHollywood(root, len, half); break;
            default: BuildDesert(root, len, half); break;
        }
        return root;
    }

    // ------------------------------------------------------------------ desert

    static void BuildDesert(Transform root, float len, float half)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            for (int j = 0; j < 2; j++)
            {
                float x = side * (half + 4f + Random.Range(0f, 18f));
                float z = -len / 4f + j * (len / 2f) + Random.Range(-3f, 3f);
                Cactus(root, new Vector3(x, 0f, z), side);
            }
        }
    }

    static void Cactus(Transform parent, Vector3 pos, int side)
    {
        var c = new GameObject("Cactus").transform;
        c.SetParent(parent, false);
        c.localPosition = pos;

        Prim.Make(PrimitiveType.Cylinder, "Trunk", c,
                  new Vector3(0f, 1.5f, 0f), new Vector3(0.6f, 1.5f, 0.6f), CactusGreen);
        Prim.Make(PrimitiveType.Cube, "ArmH", c,
                  new Vector3(-side * 0.55f, 1.6f, 0f), new Vector3(0.8f, 0.3f, 0.3f), CactusGreen);
        Prim.Make(PrimitiveType.Cube, "ArmV", c,
                  new Vector3(-side * 0.95f, 2.0f, 0f), new Vector3(0.3f, 0.8f, 0.3f), CactusGreen);
    }

    // ------------------------------------------------------------------ beach

    static void BuildBeach(Transform root, float len, float half)
    {
        // Left: a strip of sand next to the road, then the sea (the sea is the lowered ground)
        Prim.Make(PrimitiveType.Cube, "Shore", root,
                  new Vector3(-(half + 4f), -0.06f, 0f), new Vector3(8f, 0.1f, len + 0.1f), Sand);

        for (int i = 0; i < 3; i++)
        {
            Prim.Make(PrimitiveType.Cube, "Foam", root,
                      new Vector3(-(half + Random.Range(14f, 40f)), -0.13f, Random.Range(-len / 2f, len / 2f)),
                      new Vector3(0.5f, 0.02f, Random.Range(6f, 12f)), Foam);
        }

        Umbrella(root, new Vector3(-(half + Random.Range(1.8f, 6f)), 0f, Random.Range(-len / 3f, len / 3f)));

        // Right: palms on the sand
        for (int j = 0; j < 2; j++)
        {
            float x = half + Random.Range(3f, 14f);
            float z = -len / 4f + j * (len / 2f) + Random.Range(-3f, 3f);
            Palm(root, new Vector3(x, 0f, z));
        }
    }

    static void Umbrella(Transform parent, Vector3 pos)
    {
        var u = new GameObject("Umbrella").transform;
        u.SetParent(parent, false);
        u.localPosition = pos;

        var color = Umbrellas[Random.Range(0, Umbrellas.Length)];
        Prim.Make(PrimitiveType.Cylinder, "Pole", u, new Vector3(0f, 1.0f, 0f), new Vector3(0.08f, 1.0f, 0.08f), Metal);
        Prim.Make(PrimitiveType.Sphere, "Canopy", u, new Vector3(0f, 2.05f, 0f), new Vector3(2.4f, 0.6f, 2.4f), color);
        Prim.Make(PrimitiveType.Cube, "Towel", u, new Vector3(0.5f, 0.02f, 1.6f), new Vector3(1.2f, 0.03f, 2.2f), color);
    }

    // ------------------------------------------------------------------ Hollywood (rain)

    static void BuildHollywood(Transform root, float len, float half)
    {
        // Street lamps on both sides, one palm somewhere, one building per side far from the road
        for (int side = -1; side <= 1; side += 2)
        {
            Lamp(root, new Vector3(side * (half + 1.2f), 0f, side < 0 ? -8f : 7f), side);

            Building(root, new Vector3(side * (half + Random.Range(14f, 26f)), 0f, Random.Range(-8f, 8f)), side);
        }

        int palmSide = Random.value < 0.5f ? -1 : 1;
        Palm(root, new Vector3(palmSide * (half + Random.Range(4f, 10f)), 0f, Random.Range(-len / 3f, len / 3f)));

        if (Random.value < 0.25f)
        {
            int side = Random.value < 0.5f ? -1 : 1;
            Billboard(root, new Vector3(side * (half + 8f), 0f, Random.Range(-6f, 6f)));
        }
    }

    static void Lamp(Transform parent, Vector3 pos, int side)
    {
        var l = new GameObject("Lamp").transform;
        l.SetParent(parent, false);
        l.localPosition = pos;

        Prim.Make(PrimitiveType.Cylinder, "Pole", l, new Vector3(0f, 2.6f, 0f), new Vector3(0.12f, 2.6f, 0.12f), Metal);
        Prim.Make(PrimitiveType.Cube, "Arm", l, new Vector3(-side * 0.7f, 5.2f, 0f), new Vector3(1.4f, 0.1f, 0.1f), Metal);
        Prim.Make(PrimitiveType.Sphere, "Light", l, new Vector3(-side * 1.3f, 5.05f, 0f), Vector3.one * 0.45f, LampLight);
    }

    static void Building(Transform parent, Vector3 pos, int side)
    {
        float w = Random.Range(7f, 12f), d = Random.Range(8f, 12f), h = Random.Range(10f, 28f);

        var b = new GameObject("Building").transform;
        b.SetParent(parent, false);
        b.localPosition = pos;

        Prim.Make(PrimitiveType.Cube, "Block", b, new Vector3(0f, h / 2f, 0f), new Vector3(w, h, d),
                  Buildings[Random.Range(0, Buildings.Length)]);

        // Lit window strips on the side that faces the road
        int rows = Mathf.Min(5, Mathf.FloorToInt(h / 4f));
        for (int r = 0; r < rows; r++)
        {
            Prim.Make(PrimitiveType.Cube, "Windows", b,
                      new Vector3(-side * (w / 2f + 0.05f), 3f + r * 4f, 0f), new Vector3(0.1f, 0.7f, d * 0.75f), Window);
        }
    }

    static void Billboard(Transform parent, Vector3 pos)
    {
        var bb = new GameObject("Billboard").transform;
        bb.SetParent(parent, false);
        bb.localPosition = pos;

        Prim.Make(PrimitiveType.Cylinder, "PostL", bb, new Vector3(-2.2f, 3f, 0f), new Vector3(0.2f, 3f, 0.2f), Metal);
        Prim.Make(PrimitiveType.Cylinder, "PostR", bb, new Vector3(2.2f, 3f, 0f), new Vector3(0.2f, 3f, 0.2f), Metal);
        Prim.Make(PrimitiveType.Cube, "Board", bb, new Vector3(0f, 6.5f, 0f), new Vector3(6.5f, 3f, 0.3f),
                  Boards[Random.Range(0, Boards.Length)]);
        Prim.Make(PrimitiveType.Cube, "Stripe", bb, new Vector3(0f, 6.5f, -0.18f), new Vector3(4.5f, 0.5f, 0.05f), Color.white);
    }

    // ------------------------------------------------------------------ shared

    static void Palm(Transform parent, Vector3 pos)
    {
        var palm = new GameObject("Palm").transform;
        palm.SetParent(parent, false);
        palm.localPosition = pos;

        float h = Random.Range(4.5f, 6f);
        Prim.Make(PrimitiveType.Cylinder, "Trunk", palm, new Vector3(0f, h / 2f, 0f), new Vector3(0.35f, h / 2f, 0.35f), Trunk);

        var crown = new GameObject("Crown").transform;
        crown.SetParent(palm, false);
        crown.localPosition = new Vector3(0f, h, 0f);

        for (int i = 0; i < 6; i++)
        {
            var rot = Quaternion.Euler(18f, i * 60f + Random.Range(-8f, 8f), 0f);
            var leaf = Prim.Make(PrimitiveType.Cube, "Leaf", crown,
                                 rot * new Vector3(0f, 0f, 1.4f), new Vector3(0.7f, 0.08f, 2.8f), Leaf);
            leaf.transform.localRotation = rot;
        }

        Prim.Make(PrimitiveType.Sphere, "Coconut", crown, new Vector3(0.25f, -0.25f, 0.1f), Vector3.one * 0.4f, Coconut);
        Prim.Make(PrimitiveType.Sphere, "Coconut", crown, new Vector3(-0.2f, -0.28f, -0.15f), Vector3.one * 0.4f, Coconut);
    }
}
