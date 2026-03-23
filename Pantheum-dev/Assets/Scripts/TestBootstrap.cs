// HELPER DE TEST TEMPORAIRE — pas du code de jeu final.
// 1. Place ce script sur un GameObject vide dans la scène.
// 2. Assigne les références dans l'Inspector.
// 3. Lance le Play mode → des boutons apparaissent en haut à gauche de la Game view.

using System.Collections;
using UnityEngine;
using Pantheum.Buildings;
using Pantheum.Construction;
using Pantheum.Core;
using Pantheum.Units;

public class TestBootstrap : MonoBehaviour
{
    [Header("Références scène")]
    public Castle targetCastle;

    [Header("Prefabs")]
    public GameObject workerPrefab;
    public GameObject constructionSitePrefab;
    public GameObject housePrefab;

    private string _log = "...";

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 340, 500));
        GUI.Box(new Rect(0, 0, 340, 500), "");

        GUILayout.Label("TESTS DE VÉRIFICATION");
        GUILayout.Space(5);

        // ── Test 1 ─────────────────────────────────────────────────────────
        GUILayout.Label("Test 1 : vois-tu \"0 / 15\" sur le Castle sans le sélectionner ?");
        GUILayout.Space(8);

        // ── Test 2 ─────────────────────────────────────────────────────────
        if (GUILayout.Button("Test 2 — Spawn 16 Workers"))
        {
            if (!Check(targetCastle, "targetCastle") || !Check(workerPrefab, "workerPrefab")) return;
            StartCoroutine(SpawnWorkers());
        }

        // ── Test 3 + 4 ─────────────────────────────────────────────────────
        if (GUILayout.Button("Test 3+4 — Spawner un chantier de construction"))
        {
            if (!Check(constructionSitePrefab, "constructionSitePrefab") ||
                !Check(workerPrefab, "workerPrefab") ||
                !Check(targetCastle, "targetCastle")) return;
            StartCoroutine(SpawnConstruction());
        }

        // ── Test 5 ─────────────────────────────────────────────────────────
        if (GUILayout.Button("Test 5 — T2 bloqué sans Castle T2"))
        {
            bool can  = BuildingManager.Instance.CanPlace(BuildingType.ManaExtractor);
            bool tier = BuildingManager.Instance.TierRequirementMet(BuildingType.ManaExtractor);
            bool pass = !can && !tier;
            _log = $"Test 5 : CanPlace={can}  TierMet={tier}\n→ {(pass ? "PASS ✓" : "FAIL ✗ (Castle T2 déjà présent?)")}";
        }

        // ── Test 6 ─────────────────────────────────────────────────────────
        if (GUILayout.Button("Test 6 — Vérifier déverrouillage T2"))
        {
            int t2 = BuildingManager.Instance.GetCastleCount(2);
            bool can = BuildingManager.Instance.CanPlace(BuildingType.ManaExtractor);
            bool pass = t2 > 0 && can;
            _log = $"Test 6 : Castles T2={t2}  CanPlace(ManaExtractor)={can}\n→ {(pass ? "PASS ✓" : "FAIL ✗ (ajoute un Castle T2)")}";
        }

        // ── Test 7 ─────────────────────────────────────────────────────────
        if (GUILayout.Button("Test 7 — Supply (Knight oui, Worker non)"))
        {
            if (!Check(housePrefab, "housePrefab") ||
                !Check(workerPrefab, "workerPrefab") ||
                !Check(targetCastle, "targetCastle")) return;
            StartCoroutine(TestSupply());
        }

        GUILayout.Space(10);
        GUILayout.Label(_log);
        GUILayout.EndArea();
    }

    // ── Coroutines ────────────────────────────────────────────────────────

    IEnumerator SpawnConstruction()
    {
        var siteGO = Instantiate(constructionSitePrefab,
            targetCastle.transform.position + new Vector3(6f, 0, 0), Quaternion.identity);
        var site = siteGO.GetComponent<ConstructionSite>();

        var wGO = Instantiate(workerPrefab,
            targetCastle.transform.position + new Vector3(3f, 0, 0), Quaternion.identity);
        var worker = wGO.GetComponent<WorkerController>();
        worker.Initialize(targetCastle);

        yield return null; // attendre que le NavMeshAgent soit placé sur le NavMesh

        if (worker == null)
        {
            _log = "ERREUR : Worker détruit — le Castle est plein (15/15).\nRelance la scène (Stop + Play) pour reset le Castle.";
            yield break;
        }

        worker.AssignToConstruction(site);
        _log = "Test 3/4 : deux barres (verte + cyan) doivent s'avancer ensemble.\nTest 4 : à la fin un bâtiment apparait, le Worker passe en Idle.";
    }

    IEnumerator SpawnWorkers()
    {
        for (int i = 0; i < 16; i++)
        {
            Vector3 pos = targetCastle.transform.position + new Vector3(i * 1.5f, 0, 4f);
            var go = Instantiate(workerPrefab, pos, Quaternion.identity);
            go.GetComponent<WorkerController>().Initialize(targetCastle);
        }
        yield return null; // attendre que le 16e soit détruit
        int count = targetCastle.WorkerCount;
        bool pass = count == 15;
        _log = $"Test 2 : Castle = {count} / {Castle.MaxWorkers}\n→ {(pass ? "PASS ✓ le 16e a été refusé" : "FAIL ✗")}";
    }

    IEnumerator TestSupply()
    {
        Instantiate(housePrefab,
            targetCastle.transform.position + new Vector3(-4f, 0, 0), Quaternion.identity);
        yield return null; // laisser House.Awake() s'exécuter

        int total = SupplyManager.Instance.TotalSupply;
        bool knightOk = SupplyManager.Instance.TryUseSupply(1);
        int afterKnight = SupplyManager.Instance.UsedSupply;

        int beforeWorker = SupplyManager.Instance.UsedSupply;
        var wGO = Instantiate(workerPrefab,
            targetCastle.transform.position + new Vector3(0, 0, 6f), Quaternion.identity);
        wGO.GetComponent<WorkerController>().Initialize(targetCastle);
        yield return null;
        bool workerOk = SupplyManager.Instance.UsedSupply == beforeWorker;

        _log = $"Test 7 : Supply total={total} (doit être 8)\n" +
               $"Knight : supply {0}→{afterKnight} → {(knightOk ? "PASS ✓" : "FAIL ✗")}\n" +
               $"Worker : supply inchangé → {(workerOk ? "PASS ✓" : "FAIL ✗")}";
    }

    // ── Utilitaire ─────────────────────────────────────────────────────────
    bool Check(Object obj, string name)
    {
        if (obj != null) return true;
        _log = $"ERREUR : assigne \"{name}\" dans l'Inspector de TestBootstrap.";
        return false;
    }
}
