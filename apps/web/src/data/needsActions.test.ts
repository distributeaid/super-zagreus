import { apiPost, apiPatch, apiDelete } from "./apiClient";
import { revalidatePath } from "next/cache";
import { enterEdit, addNeed, updateQuantity, removeNeed, confirmNeeds } from "./needsActions";

// The server actions are thin wrappers: each hits one apiClient method with a
// fixed URL/body, then revalidates `/needs`. Mock both dependencies so we assert
// the wiring without a real fetch or Next.js cache.
vi.mock("./apiClient", () => ({ apiPost: vi.fn(), apiPatch: vi.fn(), apiDelete: vi.fn() }));
vi.mock("next/cache", () => ({ revalidatePath: vi.fn() }));

const mockPost = apiPost as unknown as ReturnType<typeof vi.fn>;
const mockPatch = apiPatch as unknown as ReturnType<typeof vi.fn>;
const mockDelete = apiDelete as unknown as ReturnType<typeof vi.fn>;
const mockRevalidate = revalidatePath as unknown as ReturnType<typeof vi.fn>;

describe("needs server actions", () => {
  beforeEach(() => {
    mockPost.mockReset().mockResolvedValue(undefined);
    mockPatch.mockReset().mockResolvedValue(undefined);
    mockDelete.mockReset().mockResolvedValue(undefined);
    mockRevalidate.mockReset();
  });

  it("enterEdit gets-or-creates the project's working draft and revalidates /needs", async () => {
    await enterEdit("p1");

    expect(mockPost).toHaveBeenCalledWith("/api/projects/p1/assessments/working-draft");
    expect(mockRevalidate).toHaveBeenCalledWith("/needs");
  });

  it("addNeed posts the item at quantity 1 and revalidates /needs", async () => {
    await addNeed("d1", "soap", "u1");

    expect(mockPost).toHaveBeenCalledWith("/api/assessments/d1/items", {
      itemTypeId: "soap",
      unitId: "u1",
      quantity: 1,
    });
    expect(mockRevalidate).toHaveBeenCalledWith("/needs");
  });

  it("updateQuantity patches the item quantity and revalidates /needs", async () => {
    await updateQuantity("d1", "i1", 7);

    expect(mockPatch).toHaveBeenCalledWith("/api/assessments/d1/items/i1", { quantity: 7 });
    expect(mockRevalidate).toHaveBeenCalledWith("/needs");
  });

  it("removeNeed deletes the item and revalidates /needs", async () => {
    await removeNeed("d1", "i1");

    expect(mockDelete).toHaveBeenCalledWith("/api/assessments/d1/items/i1");
    expect(mockRevalidate).toHaveBeenCalledWith("/needs");
  });

  it("confirmNeeds submits the draft and revalidates /needs", async () => {
    await confirmNeeds("d1");

    expect(mockPost).toHaveBeenCalledWith("/api/assessments/d1/submit");
    expect(mockRevalidate).toHaveBeenCalledWith("/needs");
  });

  it("does not revalidate when the API call rejects", async () => {
    mockPost.mockRejectedValueOnce(new Error("boom"));

    await expect(confirmNeeds("d1")).rejects.toThrow("boom");
    expect(mockRevalidate).not.toHaveBeenCalled();
  });
});
