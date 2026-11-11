/** Liveness for the Docker health check: the server is up and answering. */
export function GET() {
  return Response.json({ status: "Healthy" });
}
