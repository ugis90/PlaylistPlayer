export function extractResourcesFromResponse<T>(data: any): T[] {
  // Array of items directly
  if (Array.isArray(data)) {
    return data;
  }

  // Array of resources in resources property
  if (data?.resources) {
    return data.resources.map((item: any) => item.resource || item);
  }

  // Single or array of resources in resource property
  if (data?.resource) {
    if (Array.isArray(data.resource)) {
      return data.resource.map((item: any) => item.resource || item);
    } else {
      return [data.resource];
    }
  }

  // Try to find arrays in the response object
  if (typeof data === "object" && data !== null) {
    for (const key in data) {
      if (Array.isArray(data[key])) {
        return data[key];
      }
    }
  }

  // If nothing worked, return empty array
  return [];
}
