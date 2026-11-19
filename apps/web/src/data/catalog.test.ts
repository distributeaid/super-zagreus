import { flattenCatalog, searchCatalog } from "./catalog";

const CATEGORIES = [
  { category: "Hygiene", items: [
    { id: "soap", name: "Soap", defaultUnit: { id: "u1", name: "item" } },
    { id: "towel", name: "Towel", defaultUnit: { id: "u1", name: "item" } },
  ] },
  { category: "Food", items: [
    { id: "rice", name: "Rice", defaultUnit: { id: "u2", name: "kg" } },
  ] },
];

describe("flattenCatalog", () => {
  it("flattens categories into items carrying their category", () => {
    const flat = flattenCatalog(CATEGORIES);
    expect(flat).toHaveLength(3);
    expect(flat.find((i) => i.id === "rice")).toMatchObject({ name: "Rice", category: "Food", defaultUnit: { id: "u2", name: "kg" } });
  });
});

describe("searchCatalog", () => {
  it("filters by case-insensitive name substring", () => {
    const flat = flattenCatalog(CATEGORIES);
    expect(searchCatalog(flat, "so").map((i) => i.id)).toEqual(["soap"]);
  });

  it("returns everything for an empty query", () => {
    const flat = flattenCatalog(CATEGORIES);
    expect(searchCatalog(flat, "")).toHaveLength(3);
  });
});
