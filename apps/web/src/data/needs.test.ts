import { toNeedItems, groupByCategory, selectNeedsMode } from "./needs";

const API_ITEMS = [
  { id: "i1", itemTypeId: "soap", quantity: 3, unit: { name: "item" }, itemType: { name: "Soap", category: "Hygiene" } },
  { id: "i2", itemTypeId: "rice", quantity: 5, unit: { name: "kg" }, itemType: { name: "Rice", category: "Food" } },
];

describe("toNeedItems", () => {
  it("maps API assessment items into flat NeedItems", () => {
    expect(toNeedItems(API_ITEMS)[0]).toEqual({ id: "i1", itemTypeId: "soap", name: "Soap", category: "Hygiene", quantity: 3, unit: "item" });
  });
});

describe("groupByCategory", () => {
  it("groups needs by category, alphabetically", () => {
    const groups = groupByCategory(toNeedItems(API_ITEMS));
    expect(groups.map((g) => g.category)).toEqual(["Food", "Hygiene"]);
  });
});

describe("selectNeedsMode", () => {
  it("picks edit mode with the open draft's id when a draft exists", () => {
    expect(selectNeedsMode([{ id: "d1", status: "Draft" }, { id: "s1", status: "Submitted" }])).toEqual({ mode: "edit", draftId: "d1" });
  });

  it("picks view mode when there is no open draft", () => {
    expect(selectNeedsMode([{ id: "s1", status: "Submitted" }])).toEqual({ mode: "view" });
  });
});
