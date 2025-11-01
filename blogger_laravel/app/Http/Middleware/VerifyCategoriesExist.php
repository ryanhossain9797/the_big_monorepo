<?php

namespace App\Http\Middleware;

use Closure;
use App\Category;

class VerifyCategoriesExist
{
    /**
     * Handle an incoming request.
     *
     * @param  \Illuminate\Http\Request  $request
     * @param  \Closure  $next
     * @return mixed
     */
    public function handle($request, Closure $next)
    {
        if(Category::all()->count()==0){
            session()->flash('warning', 'No categories exist. Set up at least one category');
            return redirect(route('categories.create'));
        }
        else{
            return $next($request);
        }

    }
}
