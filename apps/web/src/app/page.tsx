import { redirect } from "next/navigation";

/** Root route: send visitors to the dashboard (the proxy guard handles auth from there). */
export default function Index() {
  redirect("/dashboard");
}
